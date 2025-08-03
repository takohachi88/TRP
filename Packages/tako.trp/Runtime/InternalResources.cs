using Trp.PostFx;
using UnityEngine;

namespace Trp
{
	[CreateAssetMenu(fileName = "InternalResources", menuName = "Rendering/Trp/InternalResources")]
	internal class InternalResources : ScriptableObject
	{
		[SerializeField] private Shader _cameraBlitShader;
		[SerializeField] private PostFxPassGroup _postFxPassGroup;

		[SerializeField] private Shader _coreBlitShader;
		[SerializeField] private Shader _coreBlitColorAndDepthShader;
		[SerializeField] private Shader _copyDepthShader;
		[SerializeField] private Shader _postProcessLutShader;

		[SerializeField] private Material _spriteUnlitMaterial;
		[SerializeField] private Material _uiMaterial;

		public Shader CameraBlitShader => _cameraBlitShader;
		public PostFxPassGroup PostFxGroup => _postFxPassGroup;

		public Shader CoreBlitShader => _coreBlitShader;
		public Shader CoreBlitColorAndDepthShader => _coreBlitColorAndDepthShader;
		public Shader CopyDepthShader => _copyDepthShader;
		public Shader PostProcessLutShader => _postProcessLutShader;

		public Material SpriteUnlitMaterial => _spriteUnlitMaterial;
		public Material UIMaterial => _uiMaterial;
	}
}