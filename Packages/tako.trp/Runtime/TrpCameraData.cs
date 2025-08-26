using System;
using System.Collections.Generic;
using TakoLib.Common.Extensions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp
{
	public enum ResolusionMode
	{
		Scale,
		Resolusion,
	}

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
		[SerializeField] private ResolusionMode _resolusionMode = ResolusionMode.Resolusion;
		[SerializeField, Range(1, 100)] private int _renderScale = 100;
		[SerializeField] private bool _bilinear = true;
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private bool _usePostx = true;
		[SerializeField] private BlendMode _blendSrc = BlendMode.One;
		[SerializeField] private BlendMode _blendDst = BlendMode.Zero;
		[SerializeField] private CustomPass[] _customPasses;

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
		}

		public bool UseOpaqueTexture => _useOpaqueTexture;
		public bool UseTransparentTexture => _useTransparentTexture;
		public bool UseDepthTexture => _useDepthTexture;
		public int RenderingLayerMask => _renderinLayerMask;
		public ResolusionMode ResolusionMode => _resolusionMode;
		public int RenderScale => _renderScale;
		public bool UseScaledRenering => _renderScale == 100;
		public bool Bilinear => _bilinear;
		public bool UseHdr => _useHdr;
		public bool UsePostx => _usePostx;

		public BlendMode BlendSrc => _blendSrc;
		public BlendMode BlendDst => _blendDst;

		public LayerMask VolumeMask => _volumeMask;
		public IReadOnlyList<CustomPass> AllCustomPasses => _customPasses;

		/// <summary>
		/// このカメラに登録されたアクティブなパスを全て実行する。
		/// </summary>
		/// <param name="phase"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		public void ExecuteCustomPasses(ref PassParams passParams, ExecutionPhase phase)
		{
			if (_customPasses.IsNullOrEmpty()) return;
			foreach (CustomPass pass in _customPasses)
			{
				if (pass.Phase == phase && pass.Enabled) pass.Execute(ref passParams);
			}
		}
	}
}