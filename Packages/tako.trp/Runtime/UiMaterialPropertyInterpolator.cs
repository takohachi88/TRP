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
		// Edit/Play Mode遷移やDomain Reload後にも元Materialを復元できるよう、参照を永続化する。
		[SerializeField, HideInInspector] private Material _sourceMaterial;
		[NonSerialized] private Material _instanceMaterial;
		[SerializeField, HideInInspector] private bool _sourceWasDefaultMaterial;

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
			_sourceMaterial = null;
			_sourceWasDefaultMaterial = false;
			EnsureMaterialInstance();
			RefreshProperties();
			Apply();
		}

		private void EnsureMaterialInstance()
		{
#if UNITY_EDITOR
			if (MaterialPropertyInterpolator.EditorMaterialCreationSuspended) return;
#endif
			if (!TryGetGraphic(out Graphic targetGraphic)) return;

			Material currentMaterial = targetGraphic.material;
			if (_instanceMaterial != null && currentMaterial == _instanceMaterial) return;

			if (_instanceMaterial != null)
			{
				// 外部からGraphicのMaterialが差し替えられた場合は、その変更を上書きしない。
				ReleaseMaterialInstance(false);
				currentMaterial = targetGraphic.material;
			}

			Material source = currentMaterial;
			if (_sourceMaterial != null &&
				(currentMaterial == null ||
				(currentMaterial == targetGraphic.defaultMaterial && !_sourceWasDefaultMaterial) ||
				(currentMaterial.hideFlags & HideFlags.DontSave) != 0))
			{
				// 一時MaterialがPlay Mode遷移で失われた場合は、永続化した元Materialから復元する。
				source = _sourceMaterial;
			}
			if (source == null) return;

			_sourceMaterial = source;
			_sourceWasDefaultMaterial = source == targetGraphic.defaultMaterial;
			_instanceMaterial = new Material(source)
			{
				name = $"{source.name} (UI Property Interpolator)",
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
				// UIシステムが設定したStencil値を壊さないよう、有効項目だけを派生Materialへ反映する。
				MaterialPropertyInterpolator.ApplyProperties(_sourceMaterial, renderingMaterial, _weight, _properties, false);
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

		/// <summary>
		/// Editorがシーンをシリアライズする前に、一時Materialを元Materialへ戻す。
		/// </summary>
		public void PrepareForEditorSerialization()
		{
			ReleaseMaterialInstance(true);
		}

		/// <summary>
		/// Editorの保存・Play Mode遷移完了後に一時Materialを再生成する。
		/// </summary>
		public void ResumeAfterEditorSerialization()
		{
			EnsureMaterialInstance();
			Apply();
		}

		private void ReleaseMaterialInstance(bool restoreSource)
		{
			if (_instanceMaterial == null)
			{
				return;
			}

			if (restoreSource && TryGetGraphic(out Graphic targetGraphic) && targetGraphic.material == _instanceMaterial)
			{
				// 元がGraphicの既定Materialだった場合はnullへ戻し、シーンに不要な参照を残さない。
				targetGraphic.material = _sourceWasDefaultMaterial ? null : _sourceMaterial;
			}

			if (Application.isPlaying) Destroy(_instanceMaterial);
			else DestroyImmediate(_instanceMaterial);

			_instanceMaterial = null;
		}
	}
}
