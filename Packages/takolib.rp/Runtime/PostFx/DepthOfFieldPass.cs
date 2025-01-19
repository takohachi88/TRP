using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using System.Security.Cryptography;

namespace TakoLib.Rp.PostFx
{
	/// <summary>
	/// 被写界深度のポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "DepthOfField")]
	internal class DepthOfFieldPass : PostFxPassBase
	{
		private static readonly int IdCoCParams = Shader.PropertyToID("_CoCParams");
		private static readonly int IdBokehKernel = Shader.PropertyToID("_BokehKernel");
		private static readonly int IdDownSampleScaleFactor = Shader.PropertyToID("_DownSampleScaleFactor");
		private static readonly int IdBokehConstants = Shader.PropertyToID("_BokehConstants");
		
		private static readonly int IdFullCoCTexture = Shader.PropertyToID("_FullCoCTexture");
		private static readonly int IdDofTexture = Shader.PropertyToID("_DofTexture");

		private int _bokehHash;
		private float _bokehMaxRadius;
		private float _bokehRcpAspect;

		private Vector4[] _bokehKernels;

		private static readonly int SampleCount = 42;

		protected override void OnInitialize()
		{
			_bokehKernels = new Vector4[SampleCount];
		}

		public class PassData
		{
			//Setup
			public Vector4[] BokehKernel;
			public int DownSample;
			public float UvMergin;
			public Vector4 CoCParams;

			//Input
			public TextureHandle Src;
			public TextureHandle DepthTexture;
			public Material Material;

			//Pass Texture
			public TextureHandle HalfCoCTexture;
			public TextureHandle FullCoCTexture;
			public TextureHandle PingTexture;
			public TextureHandle PongTexture;

			//Output Texture
			public TextureHandle Dst;

			public Camera Camera;
			public DepthOfField DepthOfField;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float GetMaxBokehRadiusInPixels(float viewportHeight)
		{
			const float RadiusInPixels = 14f;
			return Mathf.Min(0.05f, RadiusInPixels / viewportHeight);
		}

		private void PrepareBokehKernel(float maxRadius, float rcpAspect, DepthOfField depthOfField)
		{
			const int rings = 4;
			const int pointsPerRing = 7;

			int index = 0;
			float bladeCount = depthOfField.bladeCount.value;
			float curvature = 1f - depthOfField.bladeCurvature.value;
			float rotation = depthOfField.bladeRotation.value;

			for (int ring = 1; ring < rings; ring++)
			{
				float bias = 1f / pointsPerRing;
				float radius = (ring + bias) / (rings - 1f + bias);
				int points = ring * pointsPerRing;

				for (int point = 0; point < points; point++)
				{
					//point間の角度。
					float phi = 2f * Mathf.PI * point / points;

					float nt = Mathf.Cos(Mathf.PI - bladeCount);
					float dt = Mathf.Cos(phi - (Mathf.PI * 2f / bladeCount) * Mathf.Floor(bladeCount * phi + Mathf.PI) / (Mathf.PI * 2f));
					float r = radius * Mathf.Pow(nt / dt, curvature);
					float u = r * Mathf.Cos(phi - rotation);
					float v = r * Mathf.Sin(phi - rotation);

					float uRadius = u * maxRadius;
					float vRadius = v * maxRadius;
					float uRadiusPowTwo = uRadius * uRadius;
					float vRadiusPowTwo = vRadius * vRadius;
					float kernelLength = Mathf.Sqrt(uRadiusPowTwo + vRadiusPowTwo);
					float uRcp = uRadius * rcpAspect;

					_bokehKernels[index] = new(uRadius, vRadius, kernelLength, uRcp);
					index++;
				}
			}
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			DepthOfField depthOfField = volumeStack.GetComponent<DepthOfField>();
			if (!depthOfField || !depthOfField.IsActive()) return LastTarget.None;

			if (depthOfField.mode.value == DepthOfFieldMode.BokehUrp) return BokehUrp(ref passParams, depthOfField);
			else return LastTarget.None;
		}

		/// <summary>
		/// URPのBokehモードを移植。
		/// </summary>
		/// <param name="passParams"></param>
		/// <param name="depthOfField"></param>
		/// <returns></returns>
		private LastTarget BokehUrp(ref PassParams passParams, DepthOfField depthOfField)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			int downSample = 2;
			int wh = passParams.AttachmentSize.x / downSample;
			int hh = passParams.AttachmentSize.y / downSample;

			using IUnsafeRenderGraphBuilder builder = passParams.RenderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			float F = depthOfField.focalLength.value / 1000f;
			float A = depthOfField.focalLength.value / depthOfField.aperture.value;
			float P = depthOfField.focusDistance.value;
			float maxCoC = (A * F) / (P - F);
			float maxRadius = GetMaxBokehRadiusInPixels(passParams.AttachmentSize.y);
			float rcpAspect = 1f / (wh / (float)hh);

			//bokeh kernelの作成。
			//パラメータが変更されたら更新する。
			int hash = depthOfField.GetHashCode();
			if (hash != _bokehHash || maxRadius != _bokehMaxRadius || rcpAspect != _bokehRcpAspect)
			{
				_bokehHash = hash;
				_bokehMaxRadius = maxRadius;
				_bokehRcpAspect = rcpAspect;
				PrepareBokehKernel(maxRadius, rcpAspect, depthOfField);
			}

			float uvMargin = (1f / passParams.AttachmentSize.y) * downSample;

			passData.BokehKernel = _bokehKernels;
			passData.DownSample = downSample;
			passData.UvMergin = uvMargin;
			passData.CoCParams = new(P, maxCoC, maxRadius, rcpAspect);

			passData.Src = src;
			passData.Dst = dst;
			passData.DepthTexture = passParams.CameraTextures.TextureDepth;

			passData.Material = PassMaterial;

			TextureDesc desc = new(passParams.AttachmentSize.x, passParams.AttachmentSize.y)
			{
				name = "_FullCoCTexture",
				format = GraphicsFormat.R8_UNorm,
				filterMode = FilterMode.Bilinear,
				clearBuffer = true,
			};
			passData.FullCoCTexture = renderGraph.CreateTexture(desc);

			desc.name = "_PingTexture";
			desc.width = wh;
			desc.height = hh;
			desc.format = GraphicsFormat.R16G16B16A16_SFloat;
			passData.PingTexture = renderGraph.CreateTexture(desc);

			desc.name = "_PongTexture";
			passData.PongTexture = renderGraph.CreateTexture(desc);

			passData.DepthOfField = depthOfField;

			builder.UseTexture(src, AccessFlags.Read);
			builder.UseTexture(dst, AccessFlags.Write);
			builder.UseTexture(passParams.CameraTextures.TextureDepth, AccessFlags.Read);
			builder.UseTexture(passData.FullCoCTexture, AccessFlags.ReadWrite);
			builder.UseTexture(passData.PingTexture, AccessFlags.ReadWrite);
			builder.UseTexture(passData.PongTexture, AccessFlags.ReadWrite);

			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				DepthOfField depthOfField = passData.DepthOfField;
				Material material = passData.Material;
				TextureHandle src = passData.Src;
				TextureHandle dst = passData.Dst;

				material.SetVector(IdCoCParams, passData.CoCParams);
				material.SetVectorArray(IdBokehKernel, passData.BokehKernel);
				material.SetVector(IdDownSampleScaleFactor, new(1f / passData.DownSample, 1f / passData.DownSample, passData.DownSample, passData.DownSample));
				material.SetVector(IdBokehConstants, new(passData.UvMergin, passData.UvMergin * 2f));
				SetSrcSize(material, passData.Src);

				//Compute CoC
				material.SetTexture(TrpConstants.ShaderIds.CameraDepthTexture, passData.DepthTexture);
				Blitter.BlitCameraTexture(cmd, passData.Src, passData.FullCoCTexture, material, 0);

				//Downscale and Prefilter Color + CoC
				material.SetTexture(IdFullCoCTexture, passData.FullCoCTexture);
				Blitter.BlitCameraTexture(cmd, passData.Src, passData.PingTexture, material, 1);

				//Blur
				Blitter.BlitCameraTexture(cmd, passData.PingTexture, passData.PongTexture, material, 2);

				//Post Filtering
				Blitter.BlitCameraTexture(cmd, passData.PongTexture, passData.PingTexture, material, 3);

				//Composite
				material.SetTexture(IdDofTexture, passData.PingTexture);
				Blitter.BlitCameraTexture(cmd, passData.Src, passData.Dst, material, 4);
			});

			return LastTarget.Dst;
		}
	}
}