using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Trp
{
	/// <summary>
	/// アクティブシーンのシャドウ設定を上書きする。シーンに1つ配置する。
	/// </summary>
	[ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Rendering/TRP/Scene Shadow Settings")]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	public sealed class TrpSceneShadowSettings : MonoBehaviour
	{
		[Serializable]
		internal sealed class Settings
		{
			[Min(0)] public float MaxShadowDistance = 50;
			[Range(1, 4)] public int CascadeCount = 1;
			public Vector3 CascadeRatios = new(0.25f, 0.5f, 0.75f);
			[Range(0.001f, 1f)] public float DistanceFade = 0.1f;
			[Range(0.001f, 1f)] public float CascadeFade = 0.3f;
		}

		[SerializeField] private Settings _settings = new();

		// 描画中にHierarchyを検索せず、シーンを移動した場合も現在の所属先で判定する。
		private static readonly List<TrpSceneShadowSettings> EnabledOverrides = new();

		internal static Settings GetActiveSettings() => Find(SceneManager.GetActiveScene())?._settings;

		private static TrpSceneShadowSettings Find(Scene scene)
		{
			foreach (TrpSceneShadowSettings component in EnabledOverrides)
			{
				if (component && component.gameObject.scene == scene) return component;
			}
			return null;
		}

		private void OnEnable()
		{
			// 重複時は先に有効になったコンポーネントを優先する。
			if (Find(gameObject.scene))
				Debug.LogWarning("同じシーンに有効な TRP Scene Shadow Settings が複数あります。先に有効になった設定を使用します。", this);
			EnabledOverrides.Add(this);
		}

		private void OnDisable() => EnabledOverrides.Remove(this);

		private void Reset()
		{
			// 追加直後は現在のアセットと同じ見た目になるようにする。
			if (GraphicsSettings.currentRenderPipeline is not TrpAsset asset || asset.DefaultShadowSettings == null) return;
			ShadowSettings defaults = asset.DefaultShadowSettings;
			_settings.MaxShadowDistance = defaults.MaxShadowDistance;
			_settings.CascadeCount = defaults.CascadeCount;
			_settings.CascadeRatios = defaults.CascadeRatios;
			_settings.DistanceFade = defaults.DistanceFade;
			_settings.CascadeFade = defaults.CascadeFade;
			OnValidate();
		}

		private void OnValidate()
		{
			_settings.MaxShadowDistance = Mathf.Max(0f, _settings.MaxShadowDistance);
			_settings.CascadeCount = Mathf.Clamp(_settings.CascadeCount, 1, 4);
			_settings.CascadeRatios = ShadowSettings.ValidateCascadeRatios(_settings.CascadeRatios, _settings.CascadeCount);
			_settings.DistanceFade = Mathf.Clamp(_settings.DistanceFade, 0.001f, 1f);
			_settings.CascadeFade = Mathf.Clamp(_settings.CascadeFade, 0.001f, 1f);
		}
	}
}
