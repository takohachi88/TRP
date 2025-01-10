using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp.PostFx
{
	/// <summary>
	/// RadialBlurのポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "RadialBlur")]
	public class RadialBlurPass : PostFxPassBase
	{

		protected override void OnInitialize()
		{
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public LocalKeyword KeywordVignette;
			public bool UsePosterization;
			public bool UseNega;
			public bool UseVignette;
			public bool UseMosaic;
			public Posterization Posterization;
			public Nega Nega;
			public AdvancedVignette Vignette;
			public Mosaic Mosaic;
		}
		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			return LastTarget.None;
		}
	}
}