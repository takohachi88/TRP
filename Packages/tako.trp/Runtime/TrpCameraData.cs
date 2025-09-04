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
		[SerializeField] private bool _isFinalUiCamera;
		[SerializeField] private LayerMask _volumeMask = 1;
		[SerializeField] private bool _useOpaqueTexture, _useTransparentTexture, _useDepthTexture;
		[SerializeField] private int _renderinLayerMask = -1;
		[SerializeField] private bool _bilinear = true;
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private bool _usePostx = true;
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

		public bool IsFinalUiCamera => _isFinalUiCamera;
		public bool UseOpaqueTexture => _useOpaqueTexture;
		public bool UseTransparentTexture => _useTransparentTexture;
		public bool UseDepthTexture => _useDepthTexture;
		public int RenderingLayerMask => _renderinLayerMask;
		public bool Bilinear => _bilinear;
		public bool UseHdr => _useHdr;
		public bool UsePostx => _usePostx;
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