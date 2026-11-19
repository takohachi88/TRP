using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// HDRカラーをGPUで測光し、カメラごとの露出履歴を更新する。
	/// </summary>
	internal sealed class AutoExposurePass
	{
		private const int MeterSize = 16;
		private const int HistoryLifetime = 300;
		private const int CleanupInterval = 120;
		private const string HistoryName = "Auto Exposure History";
		private static readonly ProfilingSampler MeterSampler = new("Auto Exposure Meter");
		private static readonly ProfilingSampler AdaptSampler = new("Auto Exposure Adapt");
		private static readonly int IdPreviousExposure = Shader.PropertyToID("_PreviousExposure");
		private static readonly int IdExposureAdaptation = Shader.PropertyToID("_ExposureAdaptation");
		private static readonly int IdExposureHasHistory = Shader.PropertyToID("_ExposureHasHistory");

		private readonly Material _material;
		private readonly int _meterPass;
		private readonly int _adaptPass;
		private readonly GraphicsFormat _format;
		private readonly Dictionary<EntityId, History> _histories = new(4);
		private readonly List<EntityId> _staleCameras = new(4);
		private int _nextCleanupFrame;

		private sealed class History
		{
			// 同じパス内で同じテクスチャを読み書きしないよう、描画ごとに交互に使う。
			public readonly RTHandle[] Textures = new RTHandle[2];
			public int Current;
			public int LastFrame = -1;
			public uint ResetVersion;

			public void Release()
			{
				Textures[0].Release();
				Textures[1].Release();
			}
		}

		private sealed class MeterData
		{
			public TextureHandle Source;
			public Material Material;
			public int PassIndex;
		}

		private sealed class AdaptData
		{
			public TextureHandle Meter;
			public TextureHandle Previous;
			public Material Material;
			public Vector4 Adaptation;
			public int PassIndex;
		}

		public AutoExposurePass(Material material)
		{
			_material = material;
			_meterPass = material != null ? material.FindPass("Auto Exposure Meter") : -1;
			_adaptPass = material != null ? material.FindPass("Auto Exposure Adapt") : -1;
			_format = IsSupported(GraphicsFormat.R32_SFloat) ? GraphicsFormat.R32_SFloat
				: IsSupported(GraphicsFormat.R16_SFloat) ? GraphicsFormat.R16_SFloat : GraphicsFormat.None;
		}

		private static bool IsSupported(GraphicsFormat format) =>
			SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render) &&
			SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample);

		public TextureHandle RecordRenderGraph(ref PassParams passParams, TextureHandle source, VolumeStack volumeStack)
		{
			int frame = Time.frameCount;
			Cleanup(frame);

			AutoExposure settings = volumeStack.GetComponent<AutoExposure>();
			EntityId cameraId = passParams.Camera.GetEntityId();
			if (_format == GraphicsFormat.None || _meterPass < 0 || _adaptPass < 0 || !passParams.UseHdr ||
				settings == null || !settings.IsActive())
			{
				if (_histories.TryGetValue(cameraId, out History inactive)) inactive.LastFrame = -1;
				return default;
			}

			History history = GetOrCreateHistory(cameraId);
			uint resetVersion = passParams.CameraData != null ? passParams.CameraData.AutoExposureResetVersion : 0;
			RenderGraph graph = passParams.RenderGraph;
			bool canReuseHistory = Application.isPlaying && history.LastFrame >= 0 &&
				history.LastFrame >= frame - 1 && history.ResetVersion == resetVersion;
			if (canReuseHistory && history.LastFrame == frame)
				return graph.ImportTexture(history.Textures[history.Current]);

			TextureHandle meter = graph.CreateTexture(new TextureDesc(MeterSize, MeterSize)
			{
				name = MeterSampler.name,
				format = _format,
				filterMode = FilterMode.Point,
			});
			using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass(MeterSampler.name, out MeterData data, MeterSampler))
			{
				data.Source = source;
				data.Material = _material;
				data.PassIndex = _meterPass;
				builder.UseTexture(source, AccessFlags.Read);
				builder.SetRenderAttachment(meter, 0, AccessFlags.WriteAll);
				builder.SetRenderFunc(static (MeterData data, RasterGraphContext context) =>
					Blit(context.cmd, data.Source, data.Material, data.PassIndex));
			}

			TextureHandle previous = canReuseHistory ? graph.ImportTexture(history.Textures[history.Current]) : default;
			int next = 1 - history.Current;
			TextureHandle exposure = graph.ImportTexture(history.Textures[next]);
			using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass(AdaptSampler.name, out AdaptData data, AdaptSampler))
			{
				float dt = Time.unscaledDeltaTime;
				data.Meter = meter;
				data.Previous = previous;
				data.Material = _material;
				data.PassIndex = _adaptPass;
				data.Adaptation = new(-settings.maxDarkening.value, settings.maxBrightening.value,
					1f - Mathf.Exp(-dt / settings.darkToLightTime.value),
					1f - Mathf.Exp(-dt / settings.lightToDarkTime.value));
				builder.UseTexture(meter, AccessFlags.Read);
				if (previous.IsValid()) builder.UseTexture(previous, AccessFlags.Read);
				builder.SetRenderAttachment(exposure, 0, AccessFlags.WriteAll);
				builder.SetRenderFunc(static (AdaptData data, RasterGraphContext context) =>
				{
					bool hasHistory = data.Previous.IsValid();
					data.Material.SetInteger(IdExposureHasHistory, hasHistory ? 1 : 0);
					if (hasHistory) data.Material.SetTexture(IdPreviousExposure, data.Previous);
					data.Material.SetVector(IdExposureAdaptation, data.Adaptation);
					Blit(context.cmd, data.Meter, data.Material, data.PassIndex);
				});
			}

			history.Current = next;
			history.LastFrame = frame;
			history.ResetVersion = resetVersion;
			return exposure;
		}

		private static void Blit(RasterCommandBuffer cmd, TextureHandle source, Material material, int passIndex) =>
			Blitter.BlitTexture(cmd, source, Vector2.one, material, passIndex);

		private History GetOrCreateHistory(EntityId cameraId)
		{
			if (_histories.TryGetValue(cameraId, out History history)) return history;

			history = new History();
			for (int i = 0; i < history.Textures.Length; i++)
			{
				history.Textures[i] = RTHandles.Alloc(1, 1, new RTHandleAllocInfo(HistoryName)
				{
					format = _format,
					filterMode = FilterMode.Point,
					wrapModeU = TextureWrapMode.Clamp,
					wrapModeV = TextureWrapMode.Clamp,
				});
			}
			_histories.Add(cameraId, history);
			return history;
		}

		private void Cleanup(int frame)
		{
			if (frame < _nextCleanupFrame) return;
			_nextCleanupFrame = frame + CleanupInterval;
			_staleCameras.Clear();

			foreach (KeyValuePair<EntityId, History> entry in _histories)
			{
				if (entry.Value.LastFrame < frame - HistoryLifetime) _staleCameras.Add(entry.Key);
			}
			foreach (EntityId cameraId in _staleCameras)
			{
				_histories[cameraId].Release();
				_histories.Remove(cameraId);
			}
		}

		public void Dispose()
		{
			foreach (History history in _histories.Values) history.Release();
			_histories.Clear();
			_staleCameras.Clear();
		}
	}
}
