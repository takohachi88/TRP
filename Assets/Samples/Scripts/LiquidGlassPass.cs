using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.Sample
{
    [CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU + "CustomPass/" + nameof(LiquidGlassPass), fileName = nameof(LiquidGlassPass))]
    public class LiquidGlassPass : CustomPassObject
    {
		[SerializeField] private Shader _shader;
		[SerializeField, Min(3)] private int _shapeBlurSampleCount = 5;
		[SerializeField, Min(0)] private float _shapeBlurStrength = 0.1f;

		[SerializeField, Min(3)] private int _blurSampleCount = 5;
		[SerializeField, Min(0)] private float _blurStrength = 0.1f;

		[SerializeField, Range(0f, 2f)] private float _edgeDistortStrength = 0.2f;
		[SerializeField, Range(0f, 1f)] private float _uiColor = 1f;
		[SerializeField, Range(0f, 0.5f)] private float _chromaticAberrationEdgeStrength = 0.1f;
		[SerializeField, Range(0f, 0.04f)] private float _chromaticAberrationBaseStrength = 0.001f;

		[Header("Surface Lighting")]
		[SerializeField, Range(0f, 32f)] private float _surfaceNormalStrength = 8f;
		[SerializeField, Range(0f, 0.02f)] private float _surfaceNormalBlurStrength = 0.002f;
		[SerializeField, Range(0f, 4f)] private float _specularStrength = 0.65f;
		[SerializeField, Range(1f, 256f)] private float _specularPower = 48f;
		[SerializeField] private Vector2 _specularLightDirection = new(-0.4f, 0.6f);
		[SerializeField, ColorUsage(true, true)] private Color _specularColor = Color.white;

		[Header("MatCap")]
		[SerializeField] private Texture2D _matCapTexture;
		[SerializeField, Range(0f, 1f)] private float _matCapStrength = 0.5f;
		[SerializeField, ColorUsage(true, true)] private Color _matCapColor = Color.white;

		private Material _material;

		private static readonly int IdAttachmentColor = Shader.PropertyToID("_AttachmentColor");
		private static readonly int IdLiquidData = Shader.PropertyToID("_LiquidData");
		private static readonly int IdLiquidNormal = Shader.PropertyToID("_LiquidNormal");
		private static readonly int IdParams1 = Shader.PropertyToID("_Params1");
		private static readonly int IdParams2 = Shader.PropertyToID("_Params2");
		private static readonly int IdParams3 = Shader.PropertyToID("_Params3");
		private static readonly int IdParams4 = Shader.PropertyToID("_Params4");
		private static readonly int IdParams5 = Shader.PropertyToID("_Params5");
		private static readonly int IdParams6 = Shader.PropertyToID("_Params6");
		private static readonly int IdSpecularColor = Shader.PropertyToID("_SpecularColor");
		private static readonly int IdMatCapTexture = Shader.PropertyToID("_MatCapTexture");
		private static readonly int IdMatCapColor = Shader.PropertyToID("_MatCapColor");

		private static ProfilingSampler Sampler;
		private static ShaderTagId IdLiquidGlass;

		private void OnEnable()
		{
			Sampler = new(nameof(LiquidGlassPass));
			IdLiquidGlass = new("LiquidGlass");
			_material = CoreUtils.CreateEngineMaterial(_shader);
		}
		private void OnDisable()
		{
			CoreUtils.Destroy(_material);
		}

		private class PassData
		{
			public Material Material;
			public RendererListHandle RendererList;
			public TextureHandle Src;
			public TextureHandle Dst1;
			public TextureHandle Dst2;
			public TextureHandle LiquidData;
			public TextureHandle LiquidNormal;
			public bool UseSurfaceLighting;
		}

		private TextureHandle CreateTempTexture(RenderGraph renderGraph, Vector2Int size, float scale, string name, GraphicsFormat format = GraphicsFormat.R16G16B16A16_SFloat)
		{
			return renderGraph.CreateTexture(new TextureDesc((int)(size.x * scale), (int)(size.y * scale))
			{
				name = name,
				// 8-bitではぼかし結果が量子化され、疑似法線を増幅した際に段差が目立つためhalf精度を使う。
				format = format,
				filterMode = FilterMode.Bilinear,
			});
		}

		public override void Execute(ref PassParams passParams)
        {
			RenderGraph renderGraph = passParams.RenderGraph;

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Material = _material;

			_material.SetVector(IdParams1, new(_shapeBlurSampleCount, 1f / _shapeBlurSampleCount, 1f / (_shapeBlurSampleCount - 1), _shapeBlurStrength));
			_material.SetVector(IdParams2, new(_blurSampleCount, 1f / _blurSampleCount, 1f / (_blurSampleCount - 1), _blurStrength));
			_material.SetVector(IdParams3, new(_edgeDistortStrength, _uiColor, _chromaticAberrationEdgeStrength, _chromaticAberrationBaseStrength));

			// MatCapが未指定なら合成強度を0にし、既存の見た目を維持する。
			float matCapStrength = _matCapTexture != null ? _matCapStrength : 0f;
			bool useSurfaceLighting = _specularStrength > 0f || matCapStrength > 0f;
			_material.SetVector(IdParams4, new(_surfaceNormalStrength, _specularStrength, _specularPower, matCapStrength));
			_material.SetVector(IdParams5, new(1f / passParams.AttachmentSize.x, 1f / passParams.AttachmentSize.y, _specularLightDirection.x, _specularLightDirection.y));
			_material.SetVector(IdParams6, new(_surfaceNormalBlurStrength, 0f, 0f, 0f));
			_material.SetColor(IdSpecularColor, _specularColor);
			_material.SetColor(IdMatCapColor, _matCapColor);
			if (_matCapTexture != null)
			{
				_material.SetTexture(IdMatCapTexture, _matCapTexture);
			}

			passData.RendererList = renderGraph.CreateRendererList(new RendererListDesc(IdLiquidGlass, passParams.CullingResults, passParams.Camera)
			{
				layerMask = passParams.CommonSettings.TransparentLayerMask,
				renderQueueRange = RenderQueueRange.transparent,
			});
			passData.Src = passParams.CameraTextures.AttachmentColor;
			passData.Dst1 = CreateTempTexture(renderGraph, passParams.AttachmentSize, 0.5f, "LiquidGlassTemp1");
			passData.Dst2 = CreateTempTexture(renderGraph, passParams.AttachmentSize, 0.5f, "LiquidGlassTemp2");
			passData.LiquidData = CreateTempTexture(renderGraph, passParams.AttachmentSize, 1f, "LiquidGlassData");
			passData.UseSurfaceLighting = useSurfaceLighting;
			if (useSurfaceLighting)
			{
				passData.LiquidNormal = CreateTempTexture(renderGraph, passParams.AttachmentSize, 0.5f, "LiquidGlassNormal", GraphicsFormat.R16G16_SFloat);
			}

			builder.UseRendererList(passData.RendererList);
			builder.UseTexture(passData.Src, AccessFlags.ReadWrite);
			builder.UseTexture(passData.Dst1, AccessFlags.ReadWrite);
			builder.UseTexture(passData.Dst2, AccessFlags.ReadWrite);
			builder.UseTexture(passData.LiquidData, AccessFlags.ReadWrite);
			if (useSurfaceLighting)
			{
				builder.UseTexture(passData.LiquidNormal, AccessFlags.ReadWrite);
			}
			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				cmd.SetRenderTarget(passData.LiquidData);
				cmd.ClearRenderTarget(true, true, Color.clear);
				cmd.DrawRendererList(passData.RendererList);//UiLiquidGlassパスの描画。
				Blitter.BlitTexture(cmd, passData.LiquidData, passData.Dst1, passData.Material, 0);//ブラー。
				Blitter.BlitTexture(cmd, passData.Dst1, passData.Dst2, passData.Material, 1);//ブラー。
				Blitter.BlitTexture(cmd, passData.Dst2, passData.Dst1, passData.Material, 0);//ブラー。
				Blitter.BlitTexture(cmd, passData.Dst1, passData.LiquidData, passData.Material, 1);//ブラー。
				cmd.SetGlobalTexture(IdLiquidData, passData.LiquidData);

				if (passData.UseSurfaceLighting)
				{
					// 勾配は半解像度で生成・平滑化し、最終結果だけ専用バッファへ保持する。
					Blitter.BlitTexture(cmd, passData.LiquidData, passData.Dst1, passData.Material, 5);
					Blitter.BlitTexture(cmd, passData.Dst1, passData.Dst2, passData.Material, 6);
					Blitter.BlitTexture(cmd, passData.Dst2, passData.LiquidNormal, passData.Material, 7);
					cmd.SetGlobalTexture(IdLiquidNormal, passData.LiquidNormal);
				}
				cmd.SetGlobalTexture(IdAttachmentColor, passData.Src);

				Blitter.BlitTexture(cmd, passData.Src, passData.Dst1, passData.Material, 2);//ブラー。
				Blitter.BlitTexture(cmd, passData.Dst1, passData.Dst2, passData.Material, 3);//ブラー。

				Blitter.BlitTexture(cmd, passData.Dst2, passData.Src, passData.Material, 4);//Composite。
			});

		}
	}
}
