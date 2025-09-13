using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// LensFlare(data driven)のポストエフェクト。
	/// URPから移植。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "LensFlareDataDriven", fileName = nameof(LfddPass))]
	public class LfddPass : PostFxPassBase
	{
		[SerializeField] private Shader _shader;
		private Material _material;

		private static readonly ProfilingSampler SamplerDataDrivenOcclusion = ProfilingSampler.Get(TrpProfileId.LensFlareDataDrivenOcclusion);
		private static readonly ProfilingSampler SamplerDataDriven = ProfilingSampler.Get(TrpProfileId.LensFlareDataDriven);

		//TRPにはSR対応はないが、LensFlareCommonSRPが要求してくるので仕方なく。
		private XRPass _xrPass;

		protected override void OnInitialize()
		{
			_xrPass = new();
			_material = CoreUtils.CreateEngineMaterial(_shader);
		}

		protected override void OnDispose()
		{
			CoreUtils.Destroy(_material);
		}

		private class PassData
		{
			internal TextureHandle Dst;
			internal TrpCameraData CameraData;
			internal Material Material;
			internal Rect Viewport;
			internal float PaniniDistance;
			internal float PaniniCropToFit;
			internal float Width;
			internal float Height;
			internal bool UsePanini;
			internal XRPass XrPass;
			internal Camera Camera;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			if (LensFlareCommonSRP.Instance.IsEmpty() || !LensFlareCommonSRP.IsOcclusionRTCompatible() || !passParams.CameraTextures.TextureDepth.IsValid()) return LastTarget.None;

			RenderGraph renderGraph = passParams.RenderGraph;

			using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(SamplerDataDrivenOcclusion.name, out PassData passData, SamplerDataDrivenOcclusion))
			{
				RTHandle occH = LensFlareCommonSRP.occlusionRT;
				TextureHandle occlusionHandle = renderGraph.ImportTexture(LensFlareCommonSRP.occlusionRT);
				passData.Dst = occlusionHandle;
				builder.UseTexture(occlusionHandle, AccessFlags.Write);
				passData.CameraData = passParams.CameraData;
				passData.Viewport = passParams.Camera.pixelRect;
				passData.Material = _material;
				passData.Width = passParams.AttachmentSize.x;
				passData.Height = passParams.AttachmentSize.y;
				passData.XrPass = _xrPass;
				passData.Camera = passParams.Camera;
				//TODO: Paniniの対応。
				/*if (m_PaniniProjection.IsActive())
				{
					passData.UsePanini = true;
					passData.PaniniDistance = m_PaniniProjection.distance.value;
					passData.PaniniCropToFit = m_PaniniProjection.cropToFit.value;
				}
				else*/
				{
					passData.UsePanini = false;
					passData.PaniniDistance = 1.0f;
					passData.PaniniCropToFit = 1.0f;
				}

				builder.UseTexture(passParams.CameraTextures.TextureDepth, AccessFlags.Read);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					Camera camera = passData.Camera;
					Matrix4x4 nonJitteredViewProjMatrix0 = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

					LensFlareCommonSRP.ComputeOcclusion(
						passData.Material, camera, passData.XrPass, passData.XrPass.multipassId,
						passData.Width, passData.Height,
						passData.UsePanini, passData.PaniniDistance, passData.PaniniCropToFit, true,
						camera.transform.position,
						nonJitteredViewProjMatrix0,
						CommandBufferHelpers.GetNativeCommandBuffer(context.cmd),
						false, false, null, null);
				});
			}


			using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(SamplerDataDriven.name, out PassData passData, SamplerDataDriven))
			{
				passData.Dst = src;
				builder.UseTexture(src, AccessFlags.Write);
				passData.CameraData = passParams.CameraData;
				passData.Material = _material;
				passData.Width = passParams.AttachmentSize.x;
				passData.Height = passParams.AttachmentSize.y;
				passData.Viewport.x = 0.0f;
				passData.Viewport.y = 0.0f;
				TextureDesc desc = renderGraph.GetTextureDesc(src);
				passData.Viewport.width = desc.width;
				passData.Viewport.height = desc.height;
				passData.XrPass = _xrPass;
				passData.Camera = passParams.Camera;
				//TODO: Paniniの対応。
				/*if (m_PaniniProjection.IsActive())
				{
					passData.usePanini = true;
					passData.paniniDistance = m_PaniniProjection.distance.value;
					passData.paniniCropToFit = m_PaniniProjection.cropToFit.value;
				}
				else*/
				{
					passData.UsePanini = false;
					passData.PaniniDistance = 1.0f;
					passData.PaniniCropToFit = 1.0f;
				}
				if (LensFlareCommonSRP.IsOcclusionRTCompatible())
				{
					TextureHandle occlusionHandle = renderGraph.ImportTexture(LensFlareCommonSRP.occlusionRT);
					builder.UseTexture(occlusionHandle, AccessFlags.Read);
				}
				else
				{
					builder.UseTexture(passParams.CameraTextures.TextureDepth, AccessFlags.Read);
				}
				builder.AllowPassCulling(false);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					Camera camera = passData.Camera;
					var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
					Matrix4x4 nonJitteredViewProjMatrix0 = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

					LensFlareCommonSRP.DoLensFlareDataDrivenCommon(
						passData.Material, camera, passData.Viewport, passData.XrPass, passData.XrPass.multipassId,
						passData.Width, passData.Height,
						passData.UsePanini, passData.PaniniDistance, passData.PaniniCropToFit,
						true,
						camera.transform.position,
						nonJitteredViewProjMatrix0,
						CommandBufferHelpers.GetNativeCommandBuffer(context.cmd),
						false, false, null, null,
						passData.Dst,
						(Light light, Camera cam, Vector3 wo) => { return GetLensFlareLightAttenuation(light, cam, wo); },
						false);
				});
			}

			return LastTarget.Src;
		}

		private static float GetLensFlareLightAttenuation(Light light, Camera cam, Vector3 wo)
		{
			if (light != null)
			{
				switch (light.type)
				{
					case LightType.Directional:
						return LensFlareCommonSRP.ShapeAttenuationDirLight(light.transform.forward, cam.transform.forward);
					case LightType.Point:
						return LensFlareCommonSRP.ShapeAttenuationPointLight();
					case LightType.Spot:
						return LensFlareCommonSRP.ShapeAttenuationSpotConeLight(light.transform.forward, wo, light.spotAngle, light.innerSpotAngle / 180.0f);
					default:
						return 1.0f;
				}
			}
			return 1.0f;
		}
	}
}