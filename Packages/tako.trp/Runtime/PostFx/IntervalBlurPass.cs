using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Scripting.APIUpdating;

namespace Trp.PostFx
{
	/// <summary>
	/// カメラごとの履歴フレームを保持し、現在フレームへ時間方向に合成する。
	/// </summary>
	[MovedFrom(true, sourceNamespace: "Trp.PostFx", sourceAssembly: "Trp", sourceClassName: "TemporalBlurPass")]
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "IntervalBlur", fileName = nameof(IntervalBlurPass))]
	public class IntervalBlurPass : PostFxSinglePassBase
	{
		private const int HistoryCleanupInterval = 120;
		private const int HistoryStaleFrameCount = 300;
		private const string HistoryTextureName = "Interval Blur History";
		// FadeOutがごく小さくても、残像が実時間上いつまでも残らない最低減衰速度。
		private const float MinPositiveFadeRate = 0.5f;
		private const float MaxFadeRate = 10f;

		private static readonly int IdPreviousFrameTexture = Shader.PropertyToID("_PreviousFrameTexture");
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

		private readonly Dictionary<EntityId, HistoryState> _historyStates = new(4);
		private readonly List<EntityId> _staleCameraIds = new(4);
		private int _lastCleanupFrame;

		private sealed class HistoryState
		{
			public RTHandle History;
			public Vector2Int Size;
			public GraphicsFormat Format;
			public bool Valid;
			public float ElapsedTime;
			public float FadeElapsedTime;
			public int ElapsedFrames;
			public int LastRenderedFrame = -1;
		}

		private sealed class PassData
		{
			public TextureHandle Src;
			public TextureHandle History;
			public Material Material;
			public float Intensity;
		}

		protected override void OnInitialize()
		{
			ReleaseAllHistories();
			_lastCleanupFrame = Time.frameCount;
			base.OnInitialize();
		}

		protected override void OnDispose()
		{
			ReleaseAllHistories();
			base.OnDispose();
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			int frameCount = Time.frameCount;
			CleanupStaleHistories(frameCount);

			IntervalBlur intervalBlur = volumeStack.GetComponent<IntervalBlur>();
			EntityId cameraId = passParams.Camera.GetEntityId();
			if (!intervalBlur || !intervalBlur.IsActive())
			{
				InvalidateHistory(cameraId, frameCount);
				return LastTarget.None;
			}

			GraphicsFormat format = RenderingUtils.ColorFormat(passParams.UseHdr, passParams.UseAlpha);
			HistoryState state = GetOrCreateHistory(cameraId, passParams.AttachmentSize, format);
			bool isNewFrame = state.LastRenderedFrame != frameCount;

			// 描画が途切れた場合は、古い画面が突然現れないよう履歴を初期化し直す。
			if (state.Valid && state.LastRenderedFrame >= 0 && state.LastRenderedFrame < frameCount - 1)
			{
				ResetHistoryState(state);
			}

			state.LastRenderedFrame = frameCount;
			TextureHandle history = passParams.RenderGraph.ImportTexture(state.History);

			// 初回は現在フレームを履歴へ保存するだけにし、未初期化Textureとの合成を避ける。
			if (!state.Valid)
			{
				RenderingUtils.AddBlitPass(passParams.RenderGraph, src, history, false, "Initialize Interval Blur History");
				state.Valid = true;
				state.ElapsedTime = 0f;
				state.ElapsedFrames = 0;
				return LastTarget.None;
			}

			// Intervalとは独立した実時間で減衰させ、更新間隔が長い場合も残像の寿命が伸びないようにする。
			float deltaTime = isNewFrame ? Mathf.Max(0f, Time.unscaledDeltaTime) : 0f;
			state.FadeElapsedTime += deltaTime;
			bool shouldUpdateHistory = isNewFrame && AdvanceInterval(intervalBlur, state, deltaTime);
			float intensity = intervalBlur.intensity.value * GetFadeMultiplier(intervalBlur.fadeOut.value, state.FadeElapsedTime);

			using (IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler))
			{
				passData.Src = src;
				passData.History = history;
				passData.Material = PassMaterial;
				passData.Intensity = intensity;

				builder.UseTexture(src, AccessFlags.Read);
				builder.UseTexture(history, AccessFlags.Read);
				builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
				builder.SetRenderFunc<PassData>(static (data, context) =>
				{
					data.Material.SetTexture(IdPreviousFrameTexture, data.History);
					data.Material.SetFloat(IdIntensity, data.Intensity);
					Blit(context.cmd, data.Src, data.Material, 0);
				});
			}

			if (shouldUpdateHistory)
			{
				// 合成結果を戻してフィードバックさせる。ハードカットせず、FadeOutによって連続的に減衰させる。
				RenderingUtils.AddBlitPass(passParams.RenderGraph, dst, history, false, "Update Interval Blur History");
				state.ElapsedTime = 0f;
				state.FadeElapsedTime = 0f;
				state.ElapsedFrames = 0;
			}

			return LastTarget.Dst;
		}

		private HistoryState GetOrCreateHistory(EntityId cameraId, Vector2Int size, GraphicsFormat format)
		{
			if (!_historyStates.TryGetValue(cameraId, out HistoryState state))
			{
				state = new HistoryState();
				_historyStates.Add(cameraId, state);
			}

			if (state.History != null && state.Size == size && state.Format == format) return state;

			state.History?.Release();
			state.History = RTHandles.Alloc(size.x, size.y, new RTHandleAllocInfo(HistoryTextureName)
			{
				format = format,
				filterMode = FilterMode.Bilinear,
				wrapModeU = TextureWrapMode.Clamp,
				wrapModeV = TextureWrapMode.Clamp,
				msaaSamples = MSAASamples.None,
			});
			state.Size = size;
			state.Format = format;
			state.LastRenderedFrame = -1;
			ResetHistoryState(state);
			return state;
		}

		private static float GetFadeMultiplier(float fadeOut, float elapsedTime)
		{
			if (fadeOut <= 0f) return 1f;

			// 平方根カーブで低い設定値にも十分な調整幅を与える。
			float fadeRate = Mathf.Lerp(MinPositiveFadeRate, MaxFadeRate, Mathf.Sqrt(fadeOut));
			return Mathf.Exp(-fadeRate * elapsedTime);
		}

		private static bool AdvanceInterval(IntervalBlur intervalBlur, HistoryState state, float deltaTime)
		{
			switch (intervalBlur.intervalMode.value)
			{
				case IntervalBlurIntervalMode.Time:
					state.ElapsedTime += deltaTime;
					return intervalBlur.intervalTime.value <= state.ElapsedTime;
				case IntervalBlurIntervalMode.FrameCount:
					state.ElapsedFrames++;
					return intervalBlur.intervalFrameCount.value <= state.ElapsedFrames;
				default:
					return false;
			}
		}

		private void InvalidateHistory(EntityId cameraId, int frameCount)
		{
			if (!_historyStates.TryGetValue(cameraId, out HistoryState state)) return;
			ResetHistoryState(state);
			state.LastRenderedFrame = frameCount;
		}

		private static void ResetHistoryState(HistoryState state)
		{
			state.Valid = false;
			state.ElapsedTime = 0f;
			state.FadeElapsedTime = 0f;
			state.ElapsedFrames = 0;
		}

		private void CleanupStaleHistories(int frameCount)
		{
			if (frameCount - _lastCleanupFrame < HistoryCleanupInterval) return;
			_lastCleanupFrame = frameCount;
			_staleCameraIds.Clear();

			foreach (KeyValuePair<EntityId, HistoryState> pair in _historyStates)
			{
				if (pair.Value.LastRenderedFrame < frameCount - HistoryStaleFrameCount) _staleCameraIds.Add(pair.Key);
			}

			foreach (EntityId cameraId in _staleCameraIds)
			{
				_historyStates[cameraId].History?.Release();
				_historyStates.Remove(cameraId);
			}
		}

		private void ReleaseAllHistories()
		{
			foreach (HistoryState state in _historyStates.Values) state.History?.Release();
			_historyStates.Clear();
			_staleCameraIds.Clear();
		}
	}
}
