using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp
{
	/// <summary>
	/// マテリアルの数値プロパティを、元の値から指定値へ補間するコンポーネント。
	/// Animator から Weight を操作する用途を想定している。
	/// </summary>
	[ExecuteAlways, RequireComponent(typeof(Renderer)), DisallowMultipleComponent]
	public sealed class MaterialPropertyInterpolator : MonoBehaviour
	{
		public enum PropertyType
		{
			Float,
			Range,
			Integer,
			Toggle,
			Color,
			Vector,
			Texture,
			Unsupported
		}

		[Serializable]
		public sealed class Property
		{
			[SerializeField] private string _name;
			[SerializeField] private string _displayName;
			[SerializeField] private PropertyType _type;
			[SerializeField] private bool _enabled;
			[SerializeField] private float _floatValue;
			[SerializeField] private int _intValue;
			[SerializeField] private bool _toggleValue;
			[SerializeField] private bool _toggleIsInteger;
			[SerializeField] private string _keyword;
			[SerializeField, Range(0f, 1f)] private float _threshold = 0.5f;
			[SerializeField] private Color _colorValue = Color.white;
			[SerializeField] private Vector4 _vectorValue;
			[SerializeField] private Texture _textureValue;
			[SerializeField] private bool _hasScaleOffset;
			[SerializeField] private Vector2 _textureScale = Vector2.one;
			[SerializeField] private Vector2 _textureOffset;

			public string Name => _name;
			public PropertyType Type => _type;
			public bool Enabled => _enabled;

			public Property(string name, string displayName, PropertyType type, bool toggleIsInteger, string keyword,
				bool hasScaleOffset)
			{
				_name = name;
				_displayName = displayName;
				_type = type;
				_toggleIsInteger = toggleIsInteger;
				_keyword = keyword;
				_hasScaleOffset = hasScaleOffset;
			}

			public bool Matches(string name, PropertyType type)
			{
				return _name == name && _type == type;
			}

			public void UpdateMetadata(string displayName, bool toggleIsInteger, string keyword, bool hasScaleOffset,
				Material material)
			{
				bool shouldInitializeScaleOffset = !_hasScaleOffset && hasScaleOffset;
				_displayName = displayName;
				_toggleIsInteger = toggleIsInteger;
				_keyword = keyword;
				_hasScaleOffset = hasScaleOffset;
				if (shouldInitializeScaleOffset)
				{
					// NoScaleOffset指定が外れた場合や旧バージョンからの移行時は、元Materialの値で初期化する。
					_textureScale = material.GetTextureScale(_name);
					_textureOffset = material.GetTextureOffset(_name);
				}
			}

			public void InitializeValue(Material material)
			{
				switch (_type)
				{
					case PropertyType.Float:
					case PropertyType.Range:
						_floatValue = material.GetFloat(_name);
						break;
					case PropertyType.Integer:
						_intValue = material.GetInteger(_name);
						break;
					case PropertyType.Toggle:
						_toggleValue = _toggleIsInteger
							? material.GetInteger(_name) != 0
							: !Mathf.Approximately(material.GetFloat(_name), 0f);
						break;
					case PropertyType.Color:
						_colorValue = material.GetColor(_name);
						break;
					case PropertyType.Vector:
						_vectorValue = material.GetVector(_name);
						break;
					case PropertyType.Texture:
						_textureValue = material.GetTexture(_name);
						if (_hasScaleOffset)
						{
							_textureScale = material.GetTextureScale(_name);
							_textureOffset = material.GetTextureOffset(_name);
						}
						break;
				}
			}

			public void Apply(Material source, Material destination, float weight)
			{
				// 無効な項目も元の値へ戻し、Inspectorでトグルを切った結果を即座に反映する。
				float appliedWeight = _enabled ? weight : 0f;
				switch (_type)
				{
					case PropertyType.Float:
					case PropertyType.Range:
						destination.SetFloat(_name, Mathf.Lerp(source.GetFloat(_name), _floatValue, appliedWeight));
						break;
					case PropertyType.Integer:
						int value = Mathf.RoundToInt(Mathf.Lerp(source.GetInteger(_name), _intValue, appliedWeight));
						destination.SetInteger(_name, value);
						break;
					case PropertyType.Toggle:
						bool sourceToggle = _toggleIsInteger
							? source.GetInteger(_name) != 0
							: !Mathf.Approximately(source.GetFloat(_name), 0f);
						bool toggleValue = _enabled && weight >= _threshold ? _toggleValue : sourceToggle;
						if (_toggleIsInteger) destination.SetInteger(_name, toggleValue ? 1 : 0);
						else destination.SetFloat(_name, toggleValue ? 1f : 0f);

						// Toggle属性にキーワードが指定されている場合は、値とキーワードを同時に切り替える。
						if (!string.IsNullOrEmpty(_keyword))
						{
							bool keywordEnabled = _enabled && weight >= _threshold
								? _toggleValue
								: source.IsKeywordEnabled(_keyword);
							if (keywordEnabled) destination.EnableKeyword(_keyword);
							else destination.DisableKeyword(_keyword);
						}
						break;
					case PropertyType.Color:
						destination.SetColor(_name, Color.Lerp(source.GetColor(_name), _colorValue, appliedWeight));
						break;
					case PropertyType.Vector:
						destination.SetVector(_name, Vector4.Lerp(source.GetVector(_name), _vectorValue, appliedWeight));
						break;
					case PropertyType.Texture:
						Texture texture = _enabled && weight >= _threshold ? _textureValue : source.GetTexture(_name);
						destination.SetTexture(_name, texture);
						if (_hasScaleOffset)
						{
							destination.SetTextureScale(_name,
								Vector2.Lerp(source.GetTextureScale(_name), _textureScale, appliedWeight));
							destination.SetTextureOffset(_name,
								Vector2.Lerp(source.GetTextureOffset(_name), _textureOffset, appliedWeight));
						}
						break;
				}
			}
		}

		[SerializeField, Min(0)] private int _materialIndex;
		[SerializeField, Range(0f, 1f)] private float _weight;
		[SerializeField] private List<Property> _properties = new();

		[NonSerialized] private Renderer _renderer;
		// Edit/Play Mode遷移やDomain Reload後にも元Materialを復元できるよう、参照を永続化する。
		[SerializeField, HideInInspector] private Material _sourceMaterial;
		[NonSerialized] private Material _instanceMaterial;
		[NonSerialized] private int _instanceMaterialIndex = -1;
		[NonSerialized] private List<Material> _sharedMaterials;

		/// <summary>
		/// 元の値から設定値へ補間する割合。Animatorからの操作対象。
		/// </summary>
		public float Weight
		{
			get => _weight;
			set => _weight = value;
		}

		public int MaterialIndex => _materialIndex;
		public IReadOnlyList<Property> Properties => _properties;

#if UNITY_EDITOR
		/// <summary>
		/// Editorの保存・Play Mode遷移中に一時Materialの再生成を停止するためのフラグ。
		/// </summary>
		public static bool EditorMaterialCreationSuspended { get; set; }
#endif

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
		/// 現在のシェーダーからInspector表示用のプロパティ一覧を同期する。
		/// 既存項目の有効状態と補間先の値は維持する。
		/// </summary>
		public void RefreshProperties()
		{
			Material material = GetSourceMaterial();
			RefreshProperties(material, ref _properties);
		}

		/// <summary>
		/// Renderer版とUI版で共通利用するプロパティ一覧の同期処理。
		/// 一覧の再構築時だけ呼び出し、毎フレームの処理からは呼ばない。
		/// </summary>
		internal static void RefreshProperties(Material material, ref List<Property> properties)
		{
			if (material == null || material.shader == null)
			{
				// Play Mode遷移などの一時的な参照切れで、登録済みデータを消さない。
				return;
			}

			Shader shader = material.shader;
			List<Property> refreshed = new(shader.GetPropertyCount());
			for (int i = 0; i < shader.GetPropertyCount(); i++)
			{
				// ShaderGUIでも非表示の内部プロパティは操作対象に含めない。
				if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0) continue;

				string propertyName = shader.GetPropertyName(i);
				string displayName = shader.GetPropertyDescription(i);
				ShaderPropertyType shaderPropertyType = shader.GetPropertyType(i);
				bool isToggle = TryGetToggleKeyword(shader.GetPropertyAttributes(i), out string keyword);
				PropertyType propertyType = isToggle ? PropertyType.Toggle : ConvertPropertyType(shaderPropertyType);
				bool toggleIsInteger = shaderPropertyType == ShaderPropertyType.Int;
				bool hasScaleOffset = shaderPropertyType == ShaderPropertyType.Texture &&
					(shader.GetPropertyFlags(i) & ShaderPropertyFlags.NoScaleOffset) == 0;
				Property property = properties.Find(x => x.Matches(propertyName, propertyType));
				if (property == null)
				{
					property = new Property(propertyName, displayName, propertyType, toggleIsInteger, keyword, hasScaleOffset);
					property.InitializeValue(material);
				}
				else
				{
					property.UpdateMetadata(displayName, toggleIsInteger, keyword, hasScaleOffset, material);
				}

				refreshed.Add(property);
			}

			properties = refreshed;
		}

		/// <summary>
		/// Material Index変更時などに、使用中のインスタンスを作り直す。
		/// </summary>
		public void RecreateMaterialInstance()
		{
			RestoreSourceMaterial();
			// Material Index変更時は、新しいスロットのMaterialを補間元として採用する。
			_sourceMaterial = null;
			EnsureMaterialInstance();
			RefreshProperties();
			Apply();
		}

		private void EnsureMaterialInstance()
		{
#if UNITY_EDITOR
			if (EditorMaterialCreationSuspended) return;
#endif
			if (!TryGetRenderer(out Renderer targetRenderer)) return;

			List<Material> materials = GetSharedMaterials(targetRenderer);
			if (_materialIndex < 0 || _materialIndex >= materials.Count)
			{
				RestoreSourceMaterial();
				return;
			}

			if (_instanceMaterial != null &&
				_instanceMaterialIndex == _materialIndex &&
				materials[_materialIndex] == _instanceMaterial)
			{
				return;
			}

			if (_instanceMaterial != null)
			{
				// 外部からスロットが差し替えられていた場合は、その変更を上書きしない。
				bool shouldRestore = _instanceMaterialIndex >= 0 &&
					_instanceMaterialIndex < materials.Count &&
					materials[_instanceMaterialIndex] == _instanceMaterial;
				ReleaseMaterialInstance(shouldRestore);
				materials = GetSharedMaterials(targetRenderer);
			}

			Material currentMaterial = materials[_materialIndex];
			Material source = currentMaterial != null &&
				(currentMaterial.hideFlags & HideFlags.DontSave) == 0
				? currentMaterial
				: _sourceMaterial;
			if (source == null) return;

			_sourceMaterial = source;
			_instanceMaterial = new Material(source)
			{
				name = $"{source.name} (Property Interpolator)",
				hideFlags = HideFlags.HideAndDontSave
			};
			_instanceMaterialIndex = _materialIndex;
			materials[_materialIndex] = _instanceMaterial;
			targetRenderer.SetSharedMaterials(materials);
			RefreshProperties();
		}

		private void Apply()
		{
			if (_sourceMaterial == null || _instanceMaterial == null) return;

			ApplyProperties(_sourceMaterial, _instanceMaterial, _weight, _properties);
		}

		/// <summary>
		/// 指定されたMaterialへ補間結果を適用する共通処理。
		/// ListとMaterialは呼び出し側で保持し、毎フレームGCAllocを発生させない。
		/// </summary>
		internal static void ApplyProperties(Material source, Material destination, float weight, List<Property> properties,
			bool applyDisabledProperties = true)
		{
			if (source == null || destination == null) return;

			float clampedWeight = Mathf.Clamp01(weight);
			foreach (Property property in properties)
			{
				if (!applyDisabledProperties && !property.Enabled) continue;
				if (source.HasProperty(property.Name))
				{
					property.Apply(source, destination, clampedWeight);
				}
			}
		}

		private Material GetSourceMaterial()
		{
			if (_sourceMaterial != null) return _sourceMaterial;
			if (!TryGetRenderer(out Renderer targetRenderer)) return null;

			List<Material> materials = GetSharedMaterials(targetRenderer);
			return _materialIndex >= 0 && _materialIndex < materials.Count ? materials[_materialIndex] : null;
		}

		private bool TryGetRenderer(out Renderer targetRenderer)
		{
			if (_renderer == null) _renderer = GetComponent<Renderer>();
			targetRenderer = _renderer;
			return targetRenderer != null;
		}

		private List<Material> GetSharedMaterials(Renderer targetRenderer)
		{
			_sharedMaterials ??= new List<Material>();
			targetRenderer.GetSharedMaterials(_sharedMaterials);
			return _sharedMaterials;
		}

		private void RestoreSourceMaterial()
		{
			ReleaseMaterialInstance(true);
		}

		/// <summary>
		/// Editorがシーンをシリアライズする前に、一時Materialを元Materialへ戻す。
		/// 元Material参照と補間設定は保持する。
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
				_instanceMaterialIndex = -1;
				return;
			}

			if (restoreSource && TryGetRenderer(out Renderer targetRenderer))
			{
				List<Material> materials = GetSharedMaterials(targetRenderer);
				if (_instanceMaterialIndex >= 0 &&
					_instanceMaterialIndex < materials.Count &&
					materials[_instanceMaterialIndex] == _instanceMaterial)
				{
					materials[_instanceMaterialIndex] = _sourceMaterial;
					targetRenderer.SetSharedMaterials(materials);
				}
			}

			if (Application.isPlaying) Destroy(_instanceMaterial);
			else DestroyImmediate(_instanceMaterial);

			_instanceMaterial = null;
			_instanceMaterialIndex = -1;
		}

		private static PropertyType ConvertPropertyType(ShaderPropertyType type)
		{
			return type switch
			{
				ShaderPropertyType.Float => PropertyType.Float,
				ShaderPropertyType.Range => PropertyType.Range,
				ShaderPropertyType.Int => PropertyType.Integer,
				ShaderPropertyType.Color => PropertyType.Color,
				ShaderPropertyType.Vector => PropertyType.Vector,
				ShaderPropertyType.Texture => PropertyType.Texture,
				_ => PropertyType.Unsupported
			};
		}

		private static bool TryGetToggleKeyword(string[] attributes, out string keyword)
		{
			keyword = null;
			foreach (string attribute in attributes)
			{
				if (string.Equals(attribute, "Toggle", StringComparison.OrdinalIgnoreCase)) return true;
				if (!attribute.StartsWith("Toggle(", StringComparison.OrdinalIgnoreCase) || !attribute.EndsWith(")")) continue;

				keyword = attribute.Substring("Toggle(".Length, attribute.Length - "Toggle(".Length - 1).Trim();
				return true;
			}

			return false;
		}
	}
}
