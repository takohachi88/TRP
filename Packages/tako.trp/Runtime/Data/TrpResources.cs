using System;
using Trp.PostFx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Categorization;

namespace Trp
{
	/// <summary>
	/// TRPで用いるリソース。
	/// GraphicsSettingsに登録される。
	/// ランタイムで差し替えることはできない。
	/// </summary>
	[Serializable]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	[CategoryInfo(Name = "TRP Resources")]
	public class TrpResources : IRenderPipelineResources
	{
		int IRenderPipelineGraphicsSettings.version => 0;
		bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

		[SerializeField][ResourcePath("Shaders/CameraBlit.shader")] private Shader _cameraBlitShader;
		[SerializeField][ResourcePath("Runtime/Data/PostFxPassGroup.asset")] private PostFxPassGroup _postFxPassGroup;

		[SerializeField][ResourcePath("Shaders/Utilities/CoreBlit.shader")] private Shader _coreBlitShader;
		[SerializeField][ResourcePath("Shaders/Utilities/CoreBlitColorAndDepth.shader")] private Shader _coreBlitColorAndDepthShader;
		[SerializeField][ResourcePath("Shaders/Utilities/CopyDepth.shader")] private Shader _copyDepthShader;
		[SerializeField][ResourcePath("Shaders/PostFx/CreatePostFxLut.shader")] private Shader _postFxLutShader;

		[SerializeField][ResourcePath("Materials/trp_sprite_unlit.mat")] private Material _spriteUnlitMaterial;
		[SerializeField][ResourcePath("Materials/trp_ui_default.mat")] private Material _uiMaterial;

		public Shader CameraBlitShader => _cameraBlitShader;
		public PostFxPassGroup PostFxGroup => _postFxPassGroup;

		public Shader CoreBlitShader => _coreBlitShader;
		public Shader CoreBlitColorAndDepthShader => _coreBlitColorAndDepthShader;
		public Shader CopyDepthShader => _copyDepthShader;
		public Shader PostFxLutShader => _postFxLutShader;

		public Material SpriteUnlitMaterial => _spriteUnlitMaterial;
		public Material UIMaterial => _uiMaterial;
	}
}