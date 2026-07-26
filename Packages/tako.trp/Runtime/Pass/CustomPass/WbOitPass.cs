using Unity.Profiling.LowLevel;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// WbOit（Weighted Blended Order Independent Transparency）の描画。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU + "CustomPass/" + nameof(WbOitPass), fileName = nameof(WbOitPass))]
	public sealed class WbOitPass : CustomPassObject
	{
		private static readonly ProfilingSampler SamplerDraw = ProfilingSampler.Create(nameof(WbOitPass) + ".Draw", MarkerFlags.Default);
		private static readonly ProfilingSampler SamplerComposite = ProfilingSampler.Create(nameof(WbOitPass) + ".Composite", MarkerFlags.Default);
		private static readonly int IdRevealageTexture = Shader.PropertyToID("_RevealageTexture");

		[SerializeField, Range(0.1f, 1f)] private float _renderScale = 1f;

		private ShaderTagId _idWbOit;
		private Material _material;

		private void OnDisable()
		{
			CoreUtils.Destroy(_material);
			_material = null;
		}

		private void OnEnable()
		{
			// ShaderTagIdはScriptableObjectの生成後に初期化する。
			_idWbOit = new ShaderTagId("WbOit");
		}

		private bool EnsureMaterial()
		{
			if (_material) return true;

			// シェーダー参照は利用プロジェクト側のアセットへ重複して持たせず、TRPの共通リソースから取得する。
			TrpResources resources = GraphicsSettings.GetRenderPipelineSettings<TrpResources>();
			if (resources?.WbOitCompositeShader == null) return false;

			_material = CoreUtils.CreateEngineMaterial(resources.WbOitCompositeShader);
			return _material;
		}

		private class PassData
		{
			public RendererListHandle RendererListHandle;
			public TextureHandle Acculumation;
			public TextureHandle Revealage;
			public Material Material;
		}

		public override void Execute(ref PassParams passParams)
		{
			if (!EnsureMaterial()) return;

			RenderGraph renderGraph = passParams.RenderGraph;

			RendererListHandle WbOitList = renderGraph.CreateRendererList(
				new RendererListDesc(_idWbOit, passParams.CullingResults, passParams.Camera)
				{
					layerMask = passParams.CommonSettings.TransparentLayerMask,
					sortingCriteria = SortingCriteria.None,//WbOitはソート不要。
					renderQueueRange = RenderQueueRange.transparent,
					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			//TODO: WbOitなオブジェクトがないなら以下の処理をスキップする。

			int width = Mathf.Max(1, (int)(passParams.AttachmentSize.x * _renderScale));
			int height = Mathf.Max(1, (int)(passParams.AttachmentSize.y * _renderScale));
			TextureDesc desc = new(width, height);

			desc.name = "WbOitAccumulation";
			desc.format = GraphicsFormat.R16G16B16A16_SFloat;
			desc.clearBuffer = true;
			desc.clearColor = Color.clear;
			TextureHandle accumulation = renderGraph.CreateTexture(desc);

			desc.name = "WbOitRevealage";
			desc.format = GraphicsFormat.R8_UNorm;
			desc.clearColor = Color.white;
			TextureHandle revealage = renderGraph.CreateTexture(desc);

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerDraw.name, out PassData passData, SamplerDraw))
			{
				passData.Acculumation = accumulation;
				passData.Revealage = revealage;
				passData.RendererListHandle = WbOitList;
				builder.UseRendererList(WbOitList);
				builder.SetRenderAttachment(accumulation, 0, AccessFlags.Write);
				builder.SetRenderAttachment(revealage, 1, AccessFlags.Write);
				builder.AllowPassCulling(true);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					context.cmd.DrawRendererList(passData.RendererListHandle);
				});
			}

			TextureHandle cameraColor = passParams.CameraTextures.AttachmentColor;
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerComposite.name, out PassData passData, SamplerComposite))
			{
				passData.Acculumation = accumulation;
				passData.Revealage = revealage;
				passData.Material = _material;

				builder.UseTexture(accumulation, AccessFlags.Read);
				builder.UseTexture(revealage, AccessFlags.Read);
				builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
				builder.AllowPassCulling(true);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					passData.Material.SetTexture(IdRevealageTexture, passData.Revealage);
					Blitter.BlitTexture(context.cmd, passData.Acculumation, Vector2.one, passData.Material, 0);
				});
			}
		}
	}
}
