using System;
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
		[SerializeField] private LayerMask _volumeMask = 1;
		[SerializeField] private bool _useOpaqueTexture, _useTransparentTexture, _useDepthTexture;
		[SerializeField] private int _renderinLayerMask = -1;
		[SerializeField, Range(TrpConstants.RENDER_SCALE_MIN, TrpConstants.RENDER_SCALE_MAX)] private float _renderScale = 1;
		[SerializeField] private bool _bilinear = true;
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private bool _usePostx = true;
		[SerializeField] private BlendMode _blendSrc = BlendMode.One;
		[SerializeField] private BlendMode _blendDst = BlendMode.Zero;
		[SerializeField] private bool _drawShadow = true;
		[SerializeField] private bool _useOutline = true;

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

		public bool UseOpaqueTexture => _useOpaqueTexture;
		public bool UseTransparentTexture => _useTransparentTexture;
		public bool UseDepthTexture => _useDepthTexture;
		public int RenderingLayerMask => _renderinLayerMask;

		public float RenderScale => _renderScale;
		public bool Bilinear => _bilinear;
		public bool UseHdr => _useHdr;
		public bool UsePostx => _usePostx;

		public BlendMode BlendSrc => _blendSrc;
		public BlendMode BlendDst => _blendDst;

		public LayerMask VolumeMask => _volumeMask;

		public bool DrawShadow => _drawShadow;
		public bool UseOutline => _useOutline;
	}
}