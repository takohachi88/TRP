using Trp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrpEditor
{
	/// <summary>
	/// 一時MaterialがシーンやPlay Mode用バックアップへ保存されないよう、Editorのライフサイクルを管理する。
	/// </summary>
	[InitializeOnLoad]
	internal static class MaterialPropertyInterpolatorLifecycle
	{
		private static bool _playModeTransition;

		static MaterialPropertyInterpolatorLifecycle()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.quitting += OnEditorQuitting;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			EditorSceneManager.sceneSaved += OnSceneSaved;
		}

		private static void OnEditorQuitting()
		{
			MaterialPropertyInterpolator.EditorMaterialCreationSuspended = true;
			PrepareAll();
		}

		private static void OnBeforeAssemblyReload()
		{
			MaterialPropertyInterpolator.EditorMaterialCreationSuspended = true;
			PrepareAll();
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			switch (state)
			{
				case PlayModeStateChange.ExitingEditMode:
				case PlayModeStateChange.ExitingPlayMode:
					_playModeTransition = true;
					MaterialPropertyInterpolator.EditorMaterialCreationSuspended = true;
					PrepareAll();
					break;
				case PlayModeStateChange.EnteredPlayMode:
				case PlayModeStateChange.EnteredEditMode:
					_playModeTransition = false;
					MaterialPropertyInterpolator.EditorMaterialCreationSuspended = false;
					ResumeAll();
					break;
			}
		}

		private static void OnSceneSaving(Scene scene, string path)
		{
			MaterialPropertyInterpolator.EditorMaterialCreationSuspended = true;
			PrepareScene(scene);
		}

		private static void OnSceneSaved(Scene scene)
		{
			if (_playModeTransition) return;

			MaterialPropertyInterpolator.EditorMaterialCreationSuspended = false;
			// 保存処理のコールバックから抜けた後にRenderer/Graphicを更新する。
			EditorApplication.delayCall += ResumeAll;
		}

		private static void PrepareAll()
		{
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				PrepareScene(SceneManager.GetSceneAt(i));
			}
		}

		private static void PrepareScene(Scene scene)
		{
			if (!scene.IsValid() || !scene.isLoaded) return;

			foreach (GameObject root in scene.GetRootGameObjects())
			{
				foreach (MaterialPropertyInterpolator interpolator in root.GetComponentsInChildren<MaterialPropertyInterpolator>(true))
				{
					interpolator.PrepareForEditorSerialization();
				}

				foreach (UiMaterialPropertyInterpolator interpolator in root.GetComponentsInChildren<UiMaterialPropertyInterpolator>(true))
				{
					interpolator.PrepareForEditorSerialization();
				}
			}
		}

		private static void ResumeAll()
		{
			if (MaterialPropertyInterpolator.EditorMaterialCreationSuspended) return;

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);
				if (!scene.IsValid() || !scene.isLoaded) continue;

				foreach (GameObject root in scene.GetRootGameObjects())
				{
					foreach (MaterialPropertyInterpolator interpolator in root.GetComponentsInChildren<MaterialPropertyInterpolator>(true))
					{
						if (interpolator.isActiveAndEnabled) interpolator.ResumeAfterEditorSerialization();
					}

					foreach (UiMaterialPropertyInterpolator interpolator in root.GetComponentsInChildren<UiMaterialPropertyInterpolator>(true))
					{
						if (interpolator.isActiveAndEnabled) interpolator.ResumeAfterEditorSerialization();
					}
				}
			}
		}
	}
}
