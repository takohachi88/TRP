using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// カメラのフレーム間移動を、画面全体の方向性ブラーへ変換するポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "CameraMotionBlur", fileName = nameof(CameraMotionBlurPass))]
	public class CameraMotionBlurPass : PostFxSinglePassBase
	{
		private static readonly int IdBlurVector = Shader.PropertyToID("_BlurVector");
		private static readonly int IdSampleCount = Shader.PropertyToID("_SampleCount");

		private readonly Dictionary<EntityId, CameraState> _cameraStates = new();
		private LocalKeyword _keywordDither;

		private struct CameraState
		{
			public Vector3 Position;
			public Quaternion Rotation;
			public int FrameCount;
		}

		private class PassData
		{
			public TextureHandle Src;
			public Material Material;
			public Vector2 BlurVector;
			public int SampleCount;
			public bool Dither;
			public LocalKeyword KeywordDither;
		}

		protected override void OnInitialize()
		{
			base.OnInitialize();
			_keywordDither = new(PassShader, "_DITHER");
			_cameraStates.Clear();
		}

		protected override void OnDispose()
		{
			_cameraStates.Clear();
			base.OnDispose();
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			CameraMotionBlur motionBlur = volumeStack.GetComponent<CameraMotionBlur>();
			Vector2 blurVector = UpdateCameraStateAndGetBlurVector(passParams.Camera, motionBlur);
			if (!motionBlur || !motionBlur.IsActive() || blurVector.sqrMagnitude <= Mathf.Epsilon) return LastTarget.None;

			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Material = PassMaterial;
			passData.BlurVector = new(
				blurVector.x / passParams.AttachmentSize.x,
				blurVector.y / passParams.AttachmentSize.y);
			passData.SampleCount = motionBlur.sampleCount.value;
			passData.Dither = motionBlur.dither.value;
			passData.KeywordDither = _keywordDither;

			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
			builder.SetRenderFunc<PassData>(static (data, context) =>
			{
				data.Material.SetVector(IdBlurVector, data.BlurVector);
				data.Material.SetInt(IdSampleCount, data.SampleCount);
				data.Material.SetKeyword(data.KeywordDither, data.Dither);
				Blit(context.cmd, data.Src, data.Material, 0);
			});

			return LastTarget.Dst;
		}

		private Vector2 UpdateCameraStateAndGetBlurVector(Camera camera, CameraMotionBlur motionBlur)
		{
			EntityId cameraId = camera.GetEntityId();
			Transform cameraTransform = camera.transform;
			Vector3 position = cameraTransform.position;
			Quaternion rotation = cameraTransform.rotation;
			int frameCount = Time.frameCount;

			CameraState currentState = new()
			{
				Position = position,
				Rotation = rotation,
				FrameCount = frameCount,
			};

			if (!_cameraStates.TryGetValue(cameraId, out CameraState previousState) || previousState.FrameCount == frameCount)
			{
				_cameraStates[cameraId] = currentState;
				return Vector2.zero;
			}

			_cameraStates[cameraId] = currentState;
			if (!motionBlur || !motionBlur.IsActive()) return Vector2.zero;

			// 現在のカメラ空間へ変換し、奥行き方向の移動は一様な方向性を持たないため除外する。
			Vector3 localTranslation = Quaternion.Inverse(rotation) * (position - previousState.Position);
			Vector2 translationPixels = new(localTranslation.x, localTranslation.y);
			translationPixels *= motionBlur.translationSensitivity.value;

			// Rollは画面内で回転方向が位置ごとに異なるため、YawとPitchだけを使用する。
			Quaternion localRotation = Quaternion.Inverse(previousState.Rotation) * rotation;
			Vector3 deltaEuler = localRotation.eulerAngles;
			float pitch = Mathf.DeltaAngle(0f, deltaEuler.x);
			float yaw = Mathf.DeltaAngle(0f, deltaEuler.y);
			Vector2 rotationPixels = new(yaw, -pitch);
			rotationPixels *= motionBlur.rotationSensitivity.value;

			Vector2 blurPixels = (translationPixels + rotationPixels) * motionBlur.intensity.value;
			return Vector2.ClampMagnitude(blurPixels, motionBlur.maxBlurRadius.value);
		}
	}
}
