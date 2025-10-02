using System.Collections.Generic;
using TakoLib.Common.Extensions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp
{
	/// <summary>
	/// TRPにおけるカメラごとの設定。
	/// Camera型は継承できないのでこのような形に。
	/// </summary>
	[RequireComponent(typeof(Camera)), DisallowMultipleComponent, ExecuteAlways]
	public class TrpCameraData : MonoBehaviour
	{
		[SerializeField, Range(0.1f, 1f)] private float _renderScale = 1f;
		[SerializeField, Range(0.1f, 1f)] private float _oitScale = 1f;
		[SerializeField] private bool _useOit;
		[SerializeField] private LayerMask _volumeMask = 1;
		[SerializeField] private bool _useOpaqueTexture, _useTransparentTexture, _useDepthNormalsTexture;
		[SerializeField] private int _renderinLayerMask = -1;
		[SerializeField] private bool _bilinear = true;
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private bool _usePostx = true;
		[SerializeField] private bool _drawShadow = true;
		[SerializeField] private bool _useOutline = true;
		public List<CustomPass> CustomPasses;

		private string _cameraName;
		private Camera _camera;
		public Camera Camera => _camera;
		private ProfilingSampler _sampler;
		public ProfilingSampler Sampler => _sampler ?? new(CameraName);

		public string CameraName => string.IsNullOrEmpty(_cameraName) ? gameObject.name : _cameraName;


		private void Awake()
		{
			_cameraName = gameObject.name;
			_camera = GetComponent<Camera>();
			_sampler = new(CameraName);
		}

		public float RenderScale => _renderScale;
		public float OitScale => _oitScale;
		public bool UseOit => _useOit;
		public bool UseScaledRendering => !_renderScale.IsInRange(0.95f, 1.05f);
		public bool UseOpaqueTexture => _useOpaqueTexture;
		public bool UseTransparentTexture => _useTransparentTexture;
		public bool UseDepthNormalsTexture => _useDepthNormalsTexture;
		public int RenderingLayerMask => _renderinLayerMask;
		public bool Bilinear => _bilinear;
		public bool UseHdr => _useHdr;
		public bool UsePostx => _usePostx;
		public LayerMask VolumeMask => _volumeMask;
		public bool DrawShadow => _drawShadow;
		public bool UseOutline => _useOutline;

		/// <summary>
		/// このカメラに登録されたアクティブなパスを全て実行する。
		/// </summary>
		/// <param name="phase"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		public void ExecuteCustomPasses(ref PassParams passParams, ExecutionPhase phase)
		{
			if (CustomPasses.IsNullOrEmpty()) return;
			foreach (CustomPass pass in CustomPasses)
			{
				if (pass.PassObject && pass.Enabled && pass.PassObject.Phase == phase) pass.PassObject.Execute(ref passParams);
			}
		}
	}
}