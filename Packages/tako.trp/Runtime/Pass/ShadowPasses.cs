using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// ライトごとのShadow関係のデータ。
	/// </summary>
	internal struct PerLightShadowData
	{
		public float Strength;
		public float MapTileStartIndex;
		public float NormalBias;
		public static PerLightShadowData GetEmpty() => new(0, 0, 0);
		public PerLightShadowData(float strength, float mapTileStartIndex, float normalBias)
		{
			Strength = strength;
			MapTileStartIndex = mapTileStartIndex;
			NormalBias = normalBias;
		}
	}

	/// <summary>
	/// directional shadowの描画。
	/// </summary>
	internal class ShadowPasses
	{
		private static readonly ProfilingSampler SamplerDirectionalShadow = ProfilingSampler.Get(TrpProfileId.DirectionalShadow);
		private static readonly ProfilingSampler SamplerPunctualShadow = ProfilingSampler.Get(TrpProfileId.PunctualShadow);

		private int _directionalShadowCount = 0;
		private const int MAX_DIRECTIONAL_SHADOW_COUNT = 4;
		private const int MAX_CASCADE_COUNT = 4;

		private int _punctualShadowCount = 0;
		private const int MAX_PUNCTUAL_SHADOW_COUNT = 16;
		private const int MAX_TILE_COUNT_PER_LIGHT = 6;//最大はpoint lightの場合で、6個。

		private static readonly int IdDirectionalShadowMap = Shader.PropertyToID("_DirectionalShadowMap");

		//Cascadeの番号を取得する処理の最適化で用いる。
		private static readonly int IdCullingSphere0 = Shader.PropertyToID("_CullingSphere0");
		private static readonly int IdCullingSphere1 = Shader.PropertyToID("_CullingSphere1");
		private static readonly int IdCullingSphere2 = Shader.PropertyToID("_CullingSphere2");
		private static readonly int IdCullingSphere3 = Shader.PropertyToID("_CullingSphere3");
		private static readonly int IdCullingSphereRadiusSqrs = Shader.PropertyToID("_CullingSphereRadiusSqrs");
		
		private static readonly int IdCullingSphereRanges = Shader.PropertyToID("_CullingSphereRanges");
		private static readonly int IdShadowParams1 = Shader.PropertyToID("_ShadowParams1");
		private static readonly int IdWorldToDirectionalShadows = Shader.PropertyToID("_WorldToDirectionalShadows");

		private static readonly int IdPunctualShadowMap = Shader.PropertyToID("_PunctualShadowMap");

		//GPU送信------------------
		private readonly Matrix4x4[] _worldToDirectionalShadows = new Matrix4x4[MAX_DIRECTIONAL_SHADOW_COUNT * MAX_CASCADE_COUNT];
		private readonly Vector4[] _cullingSpheres = new Vector4[MAX_CASCADE_COUNT];
		//--------------------------

		private readonly DirectionalShadowData[] _directionalShadowData = new DirectionalShadowData[MAX_DIRECTIONAL_SHADOW_COUNT];
		private readonly PunctualShadowData[] _punctualShadowData = new PunctualShadowData[MAX_PUNCTUAL_SHADOW_COUNT];

		// shadow cullingで使う。
		private NativeArray<ShadowSplitData> _splitBuffer;
		private NativeArray<LightShadowCasterCullingInfo> _perLightInfos;

		public struct DirectionalShadowData
		{
			public Light Light;
			public int LightIndex;
			public PerRenderData[] PerRenderData;
			public DirectionalShadowData(Light light, int index)
			{
				Light = light;
				LightIndex = index;
				PerRenderData = new PerRenderData[MAX_CASCADE_COUNT];
			}
		}

		public struct PunctualShadowData
		{
			public Light Light;
			public int Index;
			public PerRenderData[] PerRenderData;
			public PunctualShadowData(Light light, int index)
			{
				Light = light;
				Index = index;
				PerRenderData = new PerRenderData[MAX_TILE_COUNT_PER_LIGHT];
			}
		}

		/// <summary>
		/// 一回の描画ごとのデータ。
		/// </summary>
		public struct PerRenderData
		{
			public RendererListHandle RendererList;
			public Matrix4x4 View;
			public Matrix4x4 Projection;
			public ShadowSplitData SplitData;
		}

		public void Setup(CullingResults cullingResults)
		{
			_directionalShadowCount = 0;
			_punctualShadowCount = 0;

			_perLightInfos = new(cullingResults.visibleLights.Length, Allocator.Temp);
			_splitBuffer = new(cullingResults.visibleLights.Length * MAX_TILE_COUNT_PER_LIGHT, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		}

		public PerLightShadowData RegisterDirectionalShadow(Light light, int index, CullingResults cullingResults, ShadowSettings settings)
		{
			if (MAX_DIRECTIONAL_SHADOW_COUNT < _directionalShadowCount ||
				light.shadows == LightShadows.None ||
				light.shadowStrength <= 0.00001f ||
				!cullingResults.GetShadowCasterBounds(index, out Bounds _)) return new(0, 0, 0);

			_directionalShadowData[_directionalShadowCount] = new(light, index);

			_directionalShadowCount++;

			return new(light.shadowStrength, _directionalShadowCount * settings.CascadeCount, light.shadowNormalBias);
		}

		public void RegisterPunctualShadow(Light light, int index, CullingResults cullingResults, ShadowSettings settings)
		{

		}

		/// <summary>
		/// URPから移植。
		/// </summary>
		/// <param name="projection"></param>
		/// <param name="view"></param>
		/// <returns></returns>
		static Matrix4x4 GetShadowTransform(Matrix4x4 projection, Matrix4x4 view)
		{
			//CullResults.ComputeDirectionalShadowMatricesAndCullingPrimitivesはP行列のZ反転をしないので手動で行う必要があるらしい。
			if (SystemInfo.usesReversedZBuffer)
			{
				projection.m20 = -projection.m20;
				projection.m21 = -projection.m21;
				projection.m22 = -projection.m22;
				projection.m23 = -projection.m23;
			}

			// テクスチャの座標を[-1,1] から [0,1] に。
			Matrix4x4 textureScaleAndBias = Matrix4x4.identity;
			textureScaleAndBias.m00 = 0.5f;
			textureScaleAndBias.m11 = 0.5f;
			textureScaleAndBias.m22 = 0.5f;
			textureScaleAndBias.m03 = 0.5f;
			textureScaleAndBias.m23 = 0.5f;
			textureScaleAndBias.m13 = 0.5f;

			Matrix4x4 worldToShadow = projection * view;
			return textureScaleAndBias * worldToShadow;
		}

		private class DirectionalPassData
		{
			public ShadowPasses Shadow;
			public ShadowSettings Settings;
			public int ShadowCount;
			public int SplitCount;
			public int TileSize;
			public Rect[] Viewports = new Rect[MAX_DIRECTIONAL_SHADOW_COUNT * MAX_CASCADE_COUNT];
		}

		public void RecordRenderGraph(ref PassParams passParams)
		{
			RecordRenderGraphDirectionalShadow(ref passParams);
			RecordRenderGraphPunctualShadow(ref passParams);
		}

		private void RecordRenderGraphDirectionalShadow(ref PassParams passParams)
		{
			if (_directionalShadowCount == 0) return;

			RenderGraph renderGraph = passParams.RenderGraph;
			CullingResults cullingResults = passParams.CullingResults;
			ShadowSettings settings = passParams.CommonSettings.ShadowSettings;
			int cascadeCount = settings.CascadeCount;

			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerDirectionalShadow.name, out DirectionalPassData passData, SamplerDirectionalShadow);

			//タイルの枚数に応じて、シャドウマップを縦横に何分割するか決める。
			int tileCount = _directionalShadowCount * cascadeCount;
			passData.SplitCount = tileCount switch
			{
				<= 1 => 1, //1枚なら分割無し（1x1）。
				<= 4 => 2, //4枚以下なら2x2で分割して並べる。
				_ => 4,    //5枚以上なら4x4で分割して並べる。
			};

			passData.Shadow = this;
			passData.Settings = settings;
			passData.TileSize = (int)(settings.DirectionalShadowMapSize / (float)passData.SplitCount);
			passData.ShadowCount = _directionalShadowCount;

			for (int i = 0; i < _directionalShadowCount; i++)
			{
				DirectionalShadowData shadowData = _directionalShadowData[i];
				ShadowDrawingSettings shadowDrawingSettings = new(cullingResults, shadowData.LightIndex)
				{
					//useRenderingLayerMaskTest = true,
				};

				int lightIndex = shadowData.LightIndex;
				int splitOffset = lightIndex * MAX_TILE_COUNT_PER_LIGHT;

				//ライト一つにつきcascadeの個数ぶんの処理する。つまり一回のDrawごとに必要な準備の処理ということになる。
				for (int j = 0; j < cascadeCount; j++)
				{
					int tileIndex = i * cascadeCount + j;

					cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
						activeLightIndex: lightIndex,
						splitIndex: j,
						splitCount: cascadeCount,
						splitRatio: settings.CascadeRatios,
						passData.TileSize,
						shadowData.Light.shadowNearPlane,
						out Matrix4x4 view,
						out Matrix4x4 projection,
						out ShadowSplitData splitData
						);

					Matrix4x4 worldToShadow = GetShadowTransform(projection, view);

					int offsetX = (tileIndex % passData.SplitCount) * passData.TileSize;
					int offsetY = (tileIndex / passData.SplitCount) * passData.TileSize;

					//cascadeがある場合はシャドウマップのタイルに合わせる。
					if (1 < cascadeCount)
					{
						Matrix4x4 toTile = Matrix4x4.identity;
						float sizeRcp = 1.0f / settings.DirectionalShadowMapSize;
						toTile.m00 = passData.TileSize * sizeRcp;
						toTile.m11 = passData.TileSize * sizeRcp;
						toTile.m03 = offsetX * sizeRcp;
						toTile.m13 = offsetY * sizeRcp;
						worldToShadow = toTile * worldToShadow;
					}
					passData.Viewports[tileIndex] = new(offsetX, offsetY, passData.TileSize, passData.TileSize);
					_worldToDirectionalShadows[tileIndex] = worldToShadow;

					//splitData.shadowCascadeBlendCullingFactor = 0f;

					//culling sphereはライトに関わらず一定なので、最初の一回だけでよい。
					if (i == 0) _cullingSpheres[j] = splitData.cullingSphere;

					//RendererListの取得。
					RendererListHandle rendererList = renderGraph.CreateShadowRendererList(ref shadowDrawingSettings);
					builder.UseRendererList(rendererList);

					//描画一回ごとに必要なデータ。
					_directionalShadowData[i].PerRenderData[j] = new PerRenderData
					{
						RendererList = rendererList,
						View = view,
						Projection = projection,
						SplitData = splitData,
					};

					_splitBuffer[splitOffset + j] = splitData;
				}

				//ライト1つごとに必要なデータ。
				_perLightInfos[lightIndex] = new LightShadowCasterCullingInfo
				{
					projectionType = BatchCullingProjectionType.Orthographic,
					splitRange = new RangeInt(splitOffset, settings.CascadeCount) // start, length
				};
			}

			passParams.LightingResources.DirectionalShadowMap = renderGraph.CreateTexture(new TextureDesc(settings.DirectionalShadowMapSize, settings.DirectionalShadowMapSize)
			{
				depthBufferBits = DepthBits.Depth32,
				isShadowMap = true,
				name = "Directional Shadow Map",
			});

			passParams.RenderContext.CullShadowCasters(cullingResults, new ShadowCastersCullingInfos
			{
				splitBuffer = _splitBuffer,
				perLightInfos = _perLightInfos,
			});

			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderAttachmentDepth(passParams.LightingResources.DirectionalShadowMap, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(passParams.LightingResources.DirectionalShadowMap, IdDirectionalShadowMap);
			builder.SetRenderFunc<DirectionalPassData>(static (passData, context) =>
			{
				IRasterCommandBuffer cmd = context.cmd;
				ShadowPasses shadow = passData.Shadow;
				ShadowSettings settings = passData.Settings;

				for (int i = 0; i < passData.ShadowCount; i++)
				{
					DirectionalShadowData shadowData = shadow._directionalShadowData[i];

					for (int j = 0; j < settings.CascadeCount; j++)
					{
						cmd.SetGlobalMatrixArray(IdWorldToDirectionalShadows, shadow._worldToDirectionalShadows);

						//Cascadeの番号を取得する処理の最適化。
						cmd.SetGlobalVector(IdCullingSphere0, shadow._cullingSpheres[0]);
						cmd.SetGlobalVector(IdCullingSphere1, shadow._cullingSpheres[1]);
						cmd.SetGlobalVector(IdCullingSphere2, shadow._cullingSpheres[2]);
						cmd.SetGlobalVector(IdCullingSphere3, shadow._cullingSpheres[3]);
						Vector4 cullingSphereRadiusSqrs = new(
							shadow._cullingSpheres[0].w * shadow._cullingSpheres[0].w,
							shadow._cullingSpheres[1].w * shadow._cullingSpheres[1].w,
							shadow._cullingSpheres[2].w * shadow._cullingSpheres[2].w,
							shadow._cullingSpheres[3].w * shadow._cullingSpheres[3].w
							);
						cmd.SetGlobalVector(IdCullingSphereRadiusSqrs, cullingSphereRadiusSqrs);
						cmd.SetGlobalVector(IdCullingSphereRanges, new(
							1f / cullingSphereRadiusSqrs.x,
							1f / (cullingSphereRadiusSqrs.y - cullingSphereRadiusSqrs.x),
							1f / (cullingSphereRadiusSqrs.z - cullingSphereRadiusSqrs.y),
							1f / (cullingSphereRadiusSqrs.w - cullingSphereRadiusSqrs.z)
							));

						cmd.SetGlobalVector(IdShadowParams1, new(-5f * settings.CascadeFade, settings.DistanceFade, settings.MaxShadowDistance * settings.MaxShadowDistance, settings.CascadeCount));

						PerRenderData perRenderData = shadowData.PerRenderData[j];
						int tileIndex = i * settings.CascadeCount + j;
						cmd.SetGlobalDepthBias(0, shadowData.Light.shadowBias);

						// ShadowMapは複数枚をタイル状に詰めて使うので、それぞれのViewportを設定する。左下から順に詰めていく。
						cmd.SetViewport(passData.Viewports[tileIndex]);
						cmd.SetViewProjectionMatrices(perRenderData.View, perRenderData.Projection);
						cmd.DrawRendererList(perRenderData.RendererList);
					}
				}
			});
		}

		private class PunctualPassData
		{
			public ShadowPasses Shadow;
			public ShadowSettings Settings;
			public int ShadowCount;
			public int SplitCount;
			public int TileSize;
			public Rect[] Viewports = new Rect[MAX_PUNCTUAL_SHADOW_COUNT * MAX_TILE_COUNT_PER_LIGHT];
		}

		private void RecordRenderGraphPunctualShadow(ref PassParams passParams)
		{
			if (_punctualShadowCount == 0) return;

			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(SamplerPunctualShadow.name, out DirectionalPassData passData, SamplerPunctualShadow);
			for (int i = 0; i < _punctualShadowCount; i++)
			{
				PunctualShadowData shadowData = _punctualShadowData[i];
				ShadowDrawingSettings shadowDrawingSettings = new(passParams.CullingResults, shadowData.Index)
				{
					useRenderingLayerMaskTest = true,
				};
			}
			//TODO:実装
		}
	}
}