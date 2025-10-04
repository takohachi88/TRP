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

		private Material _material;

		private static readonly int IdAttachmentColor = Shader.PropertyToID("_AttachmentColor");
		private static readonly int IdLiquidData = Shader.PropertyToID("_LiquidData");
		private static readonly int IdParams1 = Shader.PropertyToID("_Params1");
		private static readonly int IdParams2 = Shader.PropertyToID("_Params2");
		private static readonly int IdParams3 = Shader.PropertyToID("_Params3");

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
		}

		private TextureHandle CreateTempTexture(RenderGraph renderGraph, Vector2Int size, float scale, string name)
		{
			return renderGraph.CreateTexture(new TextureDesc((int)(size.x * scale), (int)(size.y * scale))
			{
				name = name,
				format = GraphicsFormat.R8G8B8A8_UNorm,
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

			passData.RendererList = renderGraph.CreateRendererList(new RendererListDesc(IdLiquidGlass, passParams.CullingResults, passParams.Camera)
			{
				renderQueueRange = RenderQueueRange.transparent,
			});
			passData.Src = passParams.CameraTextures.AttachmentColor;
			passData.Dst1 = CreateTempTexture(renderGraph, passParams.AttachmentSize, 0.5f, "LiquidGlassTemp1");
			passData.Dst2 = CreateTempTexture(renderGraph, passParams.AttachmentSize, 0.5f, "LiquidGlassTemp2");
			passData.LiquidData = CreateTempTexture(renderGraph, passParams.AttachmentSize, 1f, "LiquidGlassData");

			builder.UseRendererList(passData.RendererList);
			builder.UseTexture(passData.Src, AccessFlags.ReadWrite);
			builder.UseTexture(passData.Dst1, AccessFlags.ReadWrite);
			builder.UseTexture(passData.Dst2, AccessFlags.ReadWrite);
			builder.UseTexture(passData.LiquidData, AccessFlags.ReadWrite);
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
				cmd.SetGlobalTexture(IdAttachmentColor, passData.Src);

				Blitter.BlitTexture(cmd, passData.Src, passData.Dst1, passData.Material, 2);//ブラー。
				Blitter.BlitTexture(cmd, passData.Dst1, passData.Dst2, passData.Material, 3);//ブラー。

				Blitter.BlitTexture(cmd, passData.Dst2, passData.Src, passData.Material, 4);//Composite。
			});

		}
	}
}