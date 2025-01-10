using TakoLib.Rp.PostFx;
using UnityEngine;

namespace TakoLib.Rp
{
	[CreateAssetMenu(fileName = "InternalResources", menuName = "Rendering/Trp/InternalResources")]
	internal class InternalResources : ScriptableObject
	{
		[SerializeField] private Shader _cameraBlitShader;
		[SerializeField] private PostFxPassGroup _postFxPassGroup;

		[SerializeField] private Shader _coreBlitShader;
		[SerializeField] private Shader _coreBlitColorAndDepthShader;
		[SerializeField] private Shader _copyDepthShader;

		public Shader CameraBlitShader => _cameraBlitShader;
		public PostFxPassGroup PostFxGroup => _postFxPassGroup;

		public Shader CoreBlitShader => _coreBlitShader;
		public Shader CoreBlitColorAndDepthShader => _coreBlitColorAndDepthShader;
		public Shader CopyDepthShader => _copyDepthShader;
	}
}