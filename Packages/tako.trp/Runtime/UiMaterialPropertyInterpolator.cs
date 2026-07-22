using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Trp
{
	/// <summary>
	/// Canvas UIのGraphicが使用するマテリアルプロパティを補間するコンポーネント。
	/// AnimatorからWeightを操作する用途を想定している。
	/// </summary>
	[ExecuteAlways, RequireComponent(typeof(Graphic)), DisallowMultipleComponent]
	public sealed class UiMaterialPropertyInterpolator : MonoBehaviour
	{
		[SerializeField, Range(0f, 1f)] private float _weight;
		[SerializeField] private List<MaterialPropertyInterpolator.Property> _properties = new();

		[NonSerialized] private Graphic _graphic;
		[NonSerialized] private Material _sourceMaterial;
		[NonSerialized] private Material _instanceMaterial;
		[NonSerialized] private bool _sourceWasDefaultMaterial;

		/// <summary>
		/// 元の値から設定値へ補間する割合。Animatorからの操作対象。
		/// </summary>
		public float Weight
		{
			get => _weight;
			set => _weight = value;
		}

		public IReadOnlyList<MaterialPropertyInterpolator.Property> Properties => _properties;

		private void OnEnable()
		{
			EnsureMaterialInstance();
			Apply();
		}

		private void LateUpdate()
		{
			EnsureMaterialInstance();
			Apply();
		}

		private void OnDidApplyAnimationProperties()
		{
			// LateUpdateを待たず、Editor上でAnimatorのプレビュー結果を反映する。
			EnsureMaterialInstance();
			Apply();
		}

		private void OnDisable()
		{
			RestoreSourceMaterial();
		}

		private void OnDestroy()
		{
			RestoreSourceMaterial();
		}

		/// <summary>
		/// 現在のUIマテリアルからInspector表示用のプロパティ一覧を同期する。
		/// </summary>
		public void RefreshProperties()
		{
			MaterialPropertyInterpolator.RefreshProperties(GetSourceMaterial(), ref _properties);
		}

		/// <summary>
		/// Graphicのマテリアルが変更された場合などに専用インスタンスを作り直す。
		/// </summary>
		public void RecreateMaterialInstance()
		{
			RestoreSourceMaterial();
			EnsureMaterialInstance();
			RefreshProperties();
			Apply();
		}

		private void EnsureMaterialInstance()
		{
			if (!TryGetGraphic(out Graphic targetGraphic)) return;

			Material currentMaterial = targetGraphic.material;
			if (_instanceMaterial != null && currentMaterial == _instanceMaterial) return;

			if (_instanceMaterial != null)
			{
				// 外部からGraphicのMaterialが差し替えられた場合は、その変更を上書きしない。
				ReleaseMaterialInstance(false);
				currentMaterial = targetGraphic.material;
			}

			if (currentMaterial == null) return;

			_sourceMaterial = currentMaterial;
			_sourceWasDefaultMaterial = currentMaterial == targetGraphic.defaultMaterial;
			_instanceMaterial = new Material(currentMaterial)
			{
				name = $"{currentMaterial.name} (UI Property Interpolator)",
				hideFlags = HideFlags.HideAndDontSave
			};
			targetGraphic.material = _instanceMaterial;
			RefreshProperties();
		}

		private void Apply()
		{
			if (_sourceMaterial == null || _instanceMaterial == null || !TryGetGraphic(out Graphic targetGraphic)) return;

			// Maskなしの場合は通常このインスタンスがそのまま描画に使われる。
			// Maskありの場合に備え、ステンシル設定を加えた派生Materialにも同じ値を適用する。
			MaterialPropertyInterpolator.ApplyProperties(_sourceMaterial, _instanceMaterial, _weight, _properties);
			Material renderingMaterial = targetGraphic.materialForRendering;
			if (renderingMaterial != null && renderingMaterial != _instanceMaterial)
			{
				MaterialPropertyInterpolator.ApplyProperties(_sourceMaterial, renderingMaterial, _weight, _properties);
			}
		}

		private Material GetSourceMaterial()
		{
			if (_sourceMaterial != null) return _sourceMaterial;
			return TryGetGraphic(out Graphic targetGraphic) ? targetGraphic.material : null;
		}

		private bool TryGetGraphic(out Graphic targetGraphic)
		{
			if (_graphic == null) _graphic = GetComponent<Graphic>();
			targetGraphic = _graphic;
			return targetGraphic != null;
		}

		private void RestoreSourceMaterial()
		{
			ReleaseMaterialInstance(true);
		}

		private void ReleaseMaterialInstance(bool restoreSource)
		{
			if (_instanceMaterial == null)
			{
				_sourceMaterial = null;
				_sourceWasDefaultMaterial = false;
				return;
			}

			if (restoreSource && TryGetGraphic(out Graphic targetGraphic) && targetGraphic.material == _instanceMaterial)
			{
				// 元がGraphicの既定Materialだった場合はnullへ戻し、シーンに不要な参照を残さない。
				targetGraphic.material = _sourceWasDefaultMaterial ? null : _sourceMaterial;
			}

			if (Application.isPlaying) Destroy(_instanceMaterial);
			else DestroyImmediate(_instanceMaterial);

			_sourceMaterial = null;
			_instanceMaterial = null;
			_sourceWasDefaultMaterial = false;
		}
	}
}
