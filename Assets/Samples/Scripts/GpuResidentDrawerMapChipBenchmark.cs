using System;
using System.Collections.Generic;
using Trp;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.Samples
{
	/// <summary>
	/// mapchips.fbx内の全Meshを、実際のマップチップと同じMeshFilter + MeshRendererとして
	/// 大量配置するためのベンチマーク用コンポーネント。
	/// 
	/// GRD有効・無効の切り替えはPlay Mode開始前にTrpAssetで行う。これにより両者で
	/// 生成するGameObject、カメラ、Material構成を完全に一致させたまま計測できる。
	/// </summary>
	public sealed class GpuResidentDrawerMapChipBenchmark : MonoBehaviour
	{
		public enum LayoutMode
		{
			/// <summary>遮蔽の少ない通常の平面グリッド。</summary>
			Grid,
			/// <summary>カメラの視線方向へ同一mapchipを重ね、遮蔽カリングの効果を検証する。</summary>
			OcclusionColumns,
		}

		private const string LogPrefix = "[TRP GRD Benchmark]";
		private static readonly int MapChipGlobalTextureId = Shader.PropertyToID("_MapChipGlobalTexture");

		[Header("Map Chip Source")]
		[SerializeField] private Mesh[] _mapChipMeshes;
		[SerializeField] private Shader _mapChipShader;

		[Header("Workload")]
		[SerializeField, Min(1)] private int _instanceCount = 10000;
		[SerializeField, Min(1)] private int _warmupFrames = 240;
		[SerializeField, Min(1)] private int _sampleFrames = 600;
		[SerializeField] private bool _runOnStart = true;
		[SerializeField] private LayoutMode _layoutMode;

		private readonly List<Mesh> _validMeshes = new();
		private readonly List<double> _cpuFrameTimes = new();
		private readonly List<double> _gpuFrameTimes = new();
		private readonly List<double> _trpRenderTimes = new();
		private readonly List<long> _grdRendererCounts = new();
		private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

		private GameObject _instancesRoot;
		private readonly List<Transform> _instanceTransforms = new();
		private Material[] _materials;
		private Texture2D _globalTexture;
		private ProfilerRecorder _trpRenderRecorder;
		private ProfilerRecorder _grdRendererRecorder;
		private int _warmupFramesRemaining;
		private int _sampleFramesRemaining;
		private bool _isMeasuring;
		private int _previousVSyncCount;
		private int _previousTargetFrameRate;

		/// <summary>最後に完了した計測のサマリー。空ならまだ計測中。</summary>
		public string LastResult { get; private set; }
		public bool IsMeasuring => _isMeasuring;
		public int InstanceCount => _instanceCount;
		public int SourceMeshCount => _validMeshes.Count;

		private void Awake()
		{
			// 垂直同期による待機時間を除外し、描画提出コストの差が現れるようにする。
			_previousVSyncCount = QualitySettings.vSyncCount;
			_previousTargetFrameRate = Application.targetFrameRate;
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = -1;

			CreateSharedGlobalTexture();
			BuildInstances();
		}

		private void Start()
		{
			if (_runOnStart) StartMeasurement();
		}

		private void LateUpdate()
		{
			if (!_isMeasuring) return;

			FrameTimingManager.CaptureFrameTimings();
			if (_warmupFramesRemaining-- > 0) return;

			CollectFrameSample();
			if (--_sampleFramesRemaining <= 0) CompleteMeasurement();
		}

		private void OnDestroy()
		{
			DisposeRecorders();
			QualitySettings.vSyncCount = _previousVSyncCount;
			Application.targetFrameRate = _previousTargetFrameRate;

			if (_globalTexture) Destroy(_globalTexture);
			if (_materials == null) return;
			foreach (Material material in _materials)
			{
				if (material) Destroy(material);
			}
		}

		/// <summary>
		/// 現在のシーン配置で計測を開始する。GRD設定は本メソッドで変更しない。
		/// Play Modeに入る前にTrpAssetを切り替えてから呼び出すこと。
		/// </summary>
		public void StartMeasurement()
		{
			if (_isMeasuring || _validMeshes.Count == 0)
			{
				if (_validMeshes.Count == 0) Debug.LogError($"{LogPrefix} 有効なmapchips Meshがありません。");
				return;
			}

			_cpuFrameTimes.Clear();
			_gpuFrameTimes.Clear();
			_trpRenderTimes.Clear();
			_grdRendererCounts.Clear();
			LastResult = string.Empty;

			DisposeRecorders();
			// TRP.RenderはTrp.csのProfilingSampler。GRDカウンターはUnity 6の
			// GPU Resident Drawerモジュールが公開する実際の対象Renderer数である。
			_trpRenderRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "TRP.Render", 1);
			_grdRendererRecorder = ProfilerRecorder.StartNew(new ProfilerCategory("GPU Resident Drawer"), "GRD Renderers", 1);

			_warmupFramesRemaining = _warmupFrames;
			_sampleFramesRemaining = _sampleFrames;
			_isMeasuring = true;
			FrameTimingManager.CaptureFrameTimings();
			Debug.Log($"{LogPrefix} 計測開始: {GetDrawerMode()}, instances={_instanceCount}, meshes={_validMeshes.Count}, warmup={_warmupFrames}, samples={_sampleFrames}");
		}

		private void BuildInstances()
		{
			_instancesRoot = new GameObject("Map Chip Instances");
			_instancesRoot.transform.SetParent(transform, false);

			if (_mapChipShader == null)
			{
				Debug.LogError($"{LogPrefix} Map Chip Shaderが設定されていません。");
				return;
			}

			foreach (Mesh mesh in _mapChipMeshes)
			{
				if (mesh != null && mesh.vertexCount > 0 && mesh.subMeshCount > 0) _validMeshes.Add(mesh);
			}
			if (_validMeshes.Count == 0)
			{
				Debug.LogError($"{LogPrefix} mapchips.fbxから描画可能なMeshを取得できませんでした。");
				return;
			}

			CreateMaterialVariants();
			Bounds meshBounds = CalculateMeshBounds();
			float spacing = Mathf.Max(1f, Mathf.Max(meshBounds.size.x, meshBounds.size.z) * 1.15f);
			int columns = Mathf.CeilToInt(Mathf.Sqrt(_instanceCount));
			int rows = Mathf.CeilToInt(_instanceCount / (float)columns);
			Vector3 gridOffset = new((columns - 1) * spacing * -0.5f, -meshBounds.min.y, (rows - 1) * spacing * -0.5f);

			for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
			{
				Mesh mesh = _validMeshes[instanceIndex % _validMeshes.Count];
				Material material = _materials[instanceIndex % _materials.Length];
				int column = instanceIndex % columns;
				int row = instanceIndex / columns;

				GameObject instance = new($"MapChip_{instanceIndex:D5}");
				instance.isStatic = true;
				instance.transform.SetParent(_instancesRoot.transform, false);
				instance.transform.localPosition = gridOffset + new Vector3(column * spacing, 0f, row * spacing);
				_instanceTransforms.Add(instance.transform);

				MeshFilter filter = instance.AddComponent<MeshFilter>();
				filter.sharedMesh = mesh;

				MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
				renderer.sharedMaterials = CreateSubMeshMaterials(mesh.subMeshCount, material);
				renderer.shadowCastingMode = ShadowCastingMode.Off;
				renderer.receiveShadows = false;
				renderer.lightProbeUsage = LightProbeUsage.Off;
				renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			}

			PositionCamera(columns, rows, spacing, meshBounds);
			if (_layoutMode == LayoutMode.OcclusionColumns) ArrangeOcclusionColumns(meshBounds, spacing);
			Debug.Log($"{LogPrefix} 配置完了: instances={_instanceCount}, sourceMeshes={_validMeshes.Count}, materialVariants={_materials.Length}");
		}

		private void CreateSharedGlobalTexture()
		{
			_globalTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false, true)
			{
				name = "MapChipBenchmarkGlobalTexture",
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Repeat,
			};

			Color[] pixels = new Color[64];
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					bool light = ((x / 2) + (y / 2)) % 2 == 0;
					pixels[y * 8 + x] = light ? new Color(0.85f, 1f, 0.75f) : new Color(0.32f, 0.58f, 0.45f);
				}
			}
			_globalTexture.SetPixels(pixels);
			_globalTexture.Apply(false, true);
			Shader.SetGlobalTexture(MapChipGlobalTextureId, _globalTexture);
		}

		private void CreateMaterialVariants()
		{
			_materials = new[]
			{
				CreateMaterial("Map Chip Fast", new Color(0.76f, 0.89f, 1f), false, false),
				CreateMaterial("Map Chip Global Texture", new Color(1f, 0.92f, 0.67f), true, false),
				CreateMaterial("Map Chip Global Texture + Wind", new Color(1f, 0.68f, 0.8f), true, true),
			};
		}

		private Material CreateMaterial(string materialName, Color color, bool useGlobalTexture, bool useVertexWind)
		{
			Material material = new(_mapChipShader)
			{
				name = materialName,
				enableInstancing = true,
			};
			material.SetColor("_BaseColor", color);
			material.SetFloat("_GlobalTextureScale", 0.25f);
			material.SetFloat("_WindAmplitude", 0.08f);
			material.SetFloat("_WindFrequency", 1.5f);
			material.SetVector("_WindDirection", new Vector4(1f, 0.35f, 0f, 0f));
			SetKeyword(material, "MAPCHIP_GLOBAL_TEXTURE", useGlobalTexture);
			SetKeyword(material, "MAPCHIP_VERTEX_WIND", useVertexWind);
			return material;
		}

		private static void SetKeyword(Material material, string keyword, bool enabled)
		{
			if (enabled) material.EnableKeyword(keyword);
			else material.DisableKeyword(keyword);
		}

		private static Material[] CreateSubMeshMaterials(int subMeshCount, Material material)
		{
			Material[] materials = new Material[subMeshCount];
			for (int subMeshIndex = 0; subMeshIndex < materials.Length; subMeshIndex++) materials[subMeshIndex] = material;
			return materials;
		}

		private Bounds CalculateMeshBounds()
		{
			Bounds bounds = _validMeshes[0].bounds;
			for (int meshIndex = 1; meshIndex < _validMeshes.Count; meshIndex++) bounds.Encapsulate(_validMeshes[meshIndex].bounds);
			return bounds;
		}

		private static void PositionCamera(int columns, int rows, float spacing, Bounds meshBounds)
		{
			Camera camera = Camera.main;
			if (camera == null) return;

			float width = columns * spacing;
			float depth = rows * spacing;
			float largestDimension = Mathf.Max(width, depth);
			Vector3 target = new(0f, meshBounds.center.y, 0f);
			camera.transform.position = target + new Vector3(0f, largestDimension * 0.7f, -largestDimension * 0.8f);
			camera.transform.LookAt(target);
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = largestDimension * 3f;
			camera.fieldOfView = 55f;
		}

		/// <summary>
		/// 100個前後の画面上の列に分け、各列のmapchipをカメラの視線方向へ重ねる。
		/// 手前の同形状Meshが後方のMeshを覆うため、遮蔽カリングの有効性を測れる。
		/// </summary>
		private void ArrangeOcclusionColumns(Bounds meshBounds, float spacing)
		{
			Camera camera = Camera.main;
			if (camera == null || _instanceTransforms.Count == 0) return;

			const int TargetDepthLayers = 100;
			int columnCount = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(_instanceTransforms.Count / (float)TargetDepthLayers)), 2, 16);
			int rowCount = Mathf.CeilToInt(_instanceTransforms.Count / (float)(columnCount * TargetDepthLayers));
			int columnGroupCount = columnCount * rowCount;
			float lateralSpacing = spacing * 2.5f;
			float depthSpacing = spacing * 1.1f;
			float nearDistance = Mathf.Max(
				Mathf.Max(spacing * 4f, Mathf.Max(meshBounds.size.x, meshBounds.size.z) * 8f),
				lateralSpacing * Mathf.Max(columnCount, rowCount) * 1.5f);

			Vector3 right = camera.transform.right;
			Vector3 up = camera.transform.up;
			Vector3 forward = camera.transform.forward;
			Vector3 origin = camera.transform.position + forward * nearDistance;
			for (int index = 0; index < _instanceTransforms.Count; index++)
			{
				int groupIndex = index % columnGroupCount;
				int depthLayer = index / columnGroupCount;
				int column = groupIndex % columnCount;
				int row = groupIndex / columnCount;
				float depth = depthLayer * depthSpacing;
				// 画面上の列が奥行きによらず重なるよう、投影距離に比例して横方向を補正する。
				float perspectiveScale = (nearDistance + depth) / nearDistance;
				Vector3 lateralOffset = (right * ((column - (columnCount - 1) * 0.5f) * lateralSpacing)
					+ up * ((row - (rowCount - 1) * 0.5f) * lateralSpacing)) * perspectiveScale;
				_instanceTransforms[index].position = origin + lateralOffset + forward * depth;
			}
		}

		private void CollectFrameSample()
		{
			uint timingCount = FrameTimingManager.GetLatestTimings(1, _frameTimings);
			if (timingCount > 0)
			{
				FrameTiming timing = _frameTimings[0];
				if (timing.cpuFrameTime > 0.0) _cpuFrameTimes.Add(timing.cpuFrameTime);
				if (timing.gpuFrameTime > 0.0) _gpuFrameTimes.Add(timing.gpuFrameTime);
			}

			if (_trpRenderRecorder.Valid && _trpRenderRecorder.LastValue > 0)
			{
				// ProfilerRecorderの時間値はナノ秒。
				_trpRenderTimes.Add(_trpRenderRecorder.LastValue * 1e-6);
			}
			if (_grdRendererRecorder.Valid) _grdRendererCounts.Add(_grdRendererRecorder.LastValue);
		}

		private void CompleteMeasurement()
		{
			_isMeasuring = false;
			// Unity Profilerがカウンターをフラッシュしていない環境では0になる。
			// これはGRDへの実登録数ではないため、結果ではProfiler値として明示する。
			long grdProfilerCounterValue = _grdRendererCounts.Count > 0 ? _grdRendererCounts[_grdRendererCounts.Count - 1] : 0;
			LastResult = string.Format(
				"mode={0}, layout={1}, instances={2}, meshes={3}, materialVariants={4}, cpu={5:F3} ms ({6} samples), gpu={7}, TRP.Render={8:F3} ms, GRD profiler counter={9}",
				GetDrawerMode(),
				_layoutMode,
				_instanceCount,
				_validMeshes.Count,
				_materials?.Length ?? 0,
				Average(_cpuFrameTimes),
				_cpuFrameTimes.Count,
				_gpuFrameTimes.Count > 0 ? $"{Average(_gpuFrameTimes):F3} ms ({_gpuFrameTimes.Count} samples)" : "unavailable",
				Average(_trpRenderTimes),
				grdProfilerCounterValue);
			Debug.Log($"{LogPrefix} 計測完了: {LastResult}");
			DisposeRecorders();
		}

		private static double Average(List<double> values)
		{
			if (values.Count == 0) return 0.0;
			double sum = 0.0;
			foreach (double value in values) sum += value;
			return sum / values.Count;
		}

		private static string GetDrawerMode()
		{
			TrpAsset trpAsset = GraphicsSettings.currentRenderPipeline as TrpAsset;
			return trpAsset != null ? trpAsset.gpuResidentDrawerMode.ToString() : "TRP asset not active";
		}

		private void DisposeRecorders()
		{
			if (_trpRenderRecorder.Valid) _trpRenderRecorder.Dispose();
			if (_grdRendererRecorder.Valid) _grdRendererRecorder.Dispose();
		}
	}
}
