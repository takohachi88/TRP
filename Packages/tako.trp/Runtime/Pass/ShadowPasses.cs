using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
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
		public LightType? Type;
		public float Strength;
		public float MapTileStartIndex;
		public float NormalBias;
		public static PerLightShadowData Empty => new(null, 0, 0, 0);
		public PerLightShadowData(LightType? type, float strength, float mapTileStartIndex, float normalBias)
		{
			Type = type;
			Strength = strength;
			MapTileStartIndex = mapTileStartIndex;
			NormalBias = normalBias;
		}
	}

	/// <summary>
	/// 点光源ライトのGPU送信用バッファ。
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct PunctualShadowTileBuffer
	{
		//このstructが何バイトか？
		public const int STRIDE = sizeof(float) * 4 + sizeof(float) * 16;

		public float4 TileData;
		public Matrix4x4 WorldToShadow;
		public PunctualShadowTileBuffer(Vector2 offset, float scale, float bias, float border, Matrix4x4 worldToShadow)
		{
			TileData.x = offset.x * scale + border;
			TileData.y = offset.y * scale + border;
			TileData.z = scale - border - border;
			TileData.w = bias;
			WorldToShadow = worldToShadow;
		}
	}

	/// <summary>
	/// directional shadowの描画。
	/// </summary>
	internal class ShadowPasses
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Shadow);

		private int _directionalShadowCount = 0;
		private const int MAX_DIRECTIONAL_SHADOW_COUNT = 4;
		private const int MAX_CASCADE_COUNT = 4;

		//spot lightは1枚、point lightは6枚タイルを使うので、「影を持つライトの個数」というよりは「必要なタイルの枚数」でやりくりする。
		private int _punctualTileCount = 0;
		private const int MAX_PUNCTUAL_TILE_COUNT = 25;
		private const int POINT_TILE_CONSUME_COUNT = 6;
		/// <summary>
		/// ライト一つあたりの、シャドウマップに占めるタイルの最大枚数。最大はpoint lightの場合で、6個。
		/// </summary>
		private const int MAX_TILE_COUNT_PER_LIGHT = POINT_TILE_CONSUME_COUNT;
		private static int PunctualTileConsumeCount(LightType type)
			=> type switch
			{
				LightType.Spot => 1,
				LightType.Point => POINT_TILE_CONSUME_COUNT,
				_ => throw new ArgumentException(),
			};

		/// <summary>
		/// タイルの枚数に応じて、シャドウマップを縦横に何分割するか決める。
		/// </summary>
		private static int MapSplitCount(int tileCount)
			=> tileCount switch
			{
				<= 1 => 1, //1枚なら分割無し（1x1）。
				<= 4 => 2, //4枚以下なら2x2で分割して並べる。
				<= 16 => 4,//16枚以下なら4x4で分割して並べる。
				<= 25 => 5,//25枚以下なら5x5で分割して並べる。
				_ => throw new ArgumentException(),
			};

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
		private static readonly int IdPunctualShadowTileBuffer = Shader.PropertyToID("_PunctualShadowTileBuffer");

		//GPU送信------------------
		private readonly Matrix4x4[] _worldToDirectionalShadows = new Matrix4x4[MAX_DIRECTIONAL_SHADOW_COUNT * MAX_CASCADE_COUNT];
		private readonly Vector4[] _cullingSpheres = new Vector4[MAX_CASCADE_COUNT];
		private readonly PunctualShadowTileBuffer[] _punctualShadowTileBuffer = new PunctualShadowTileBuffer[MAX_PUNCTUAL_TILE_COUNT];
		//--------------------------

		private readonly PunctualShadowData[] _punctualShadowData = new PunctualShadowData[MAX_PUNCTUAL_TILE_COUNT];
		private readonly DirectionalShadowData[] _directionalShadowData = new DirectionalShadowData[MAX_DIRECTIONAL_SHADOW_COUNT];

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
			public int LightIndex;
			public PerRenderData[] PerRenderData;
			public PunctualShadowData(Light light, int index)
			{
				Light = light;
				LightIndex = index;
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
			public Rect Viewport;
		}

		public void Setup(CullingResults cullingResults)
		{
			_directionalShadowCount = 0;
			_punctualTileCount = 0;

			_perLightInfos = new(cullingResults.visibleLights.Length, Allocator.Temp);
			_splitBuffer = new(cullingResults.visibleLights.Length * MAX_TILE_COUNT_PER_LIGHT, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		}

		public PerLightShadowData RegisterDirectionalShadow(Light light, int index, CullingResults cullingResults, ShadowSettings settings)
		{
			if (MAX_DIRECTIONAL_SHADOW_COUNT <= _directionalShadowCount ||
				light.shadows == LightShadows.None ||
				light.shadowStrength <= 0.00001f ||
				!cullingResults.GetShadowCasterBounds(index, out Bounds _)) return PerLightShadowData.Empty;

			_directionalShadowData[_directionalShadowCount] = new(light, index);
			int mapTileStartIndex = _directionalShadowCount * settings.CascadeCount;
			_directionalShadowCount++;
			return new(LightType.Directional, light.shadowStrength, mapTileStartIndex, light.shadowNormalBias);
		}

		public PerLightShadowData RegisterPunctualShadow(Light light, int index, CullingResults cullingResults, ShadowSettings settings)
		{
			int afterPunctualTileCount = _punctualTileCount + PunctualTileConsumeCount(light.type);

			if (MAX_PUNCTUAL_TILE_COUNT <= afterPunctualTileCount ||
				light.shadows == LightShadows.None ||
				light.shadowStrength <= 0.00001f ||
				!cullingResults.GetShadowCasterBounds(index, out Bounds _)) return PerLightShadowData.Empty;

			_punctualShadowData[_punctualTileCount] = new(light, index);
			PerLightShadowData output = new(light.type, light.shadowStrength, _punctualTileCount, light.shadowNormalBias);
			_punctualTileCount = afterPunctualTileCount;
			return output;
		}

		/// <summary>
		/// URPから移植、改造。
		/// </summary>
		/// <param name="projection"></param>
		/// <param name="view"></param>
		/// <param name="offset"></param>
		/// <param name="mapSizeRcp"></param>
		/// <param name="tileSize"></param>
		/// <returns></returns>
		static Matrix4x4 GetShadowTransform(Matrix4x4 projection, Matrix4x4 view, Vector2 offset, float mapSizeRcp, float tileSize)
		{
			//P行列のZ反転は手動で行う必要がある。
			if (SystemInfo.usesReversedZBuffer)
			{
				projection.m20 = -projection.m20;
				projection.m21 = -projection.m21;
				projection.m22 = -projection.m22;
				projection.m23 = -projection.m23;
			}

			Matrix4x4 worldToShadow = projection * view;

			// テクスチャの座標を[-1,1] から [0,1] に。
			Matrix4x4 textureScaleAndBias = Matrix4x4.identity;
			textureScaleAndBias.m00 = 0.5f;
			textureScaleAndBias.m11 = 0.5f;
			textureScaleAndBias.m22 = 0.5f;
			textureScaleAndBias.m03 = 0.5f;
			textureScaleAndBias.m23 = 0.5f;
			textureScaleAndBias.m13 = 0.5f;

			worldToShadow = textureScaleAndBias * worldToShadow;

			//タイルに合わせる。
			Matrix4x4 mapToTile = Matrix4x4.identity;
			mapToTile.m00 = tileSize * mapSizeRcp;
			mapToTile.m11 = tileSize * mapSizeRcp;
			mapToTile.m03 = offset.x * mapSizeRcp;
			mapToTile.m13 = offset.y * mapSizeRcp;

			return mapToTile * worldToShadow;
		}

		private class PassData
		{
			public ShadowPasses ShadowPasses;
			public ShadowSettings Settings;
			public int DirectionalShadowCount;
			public int DirectionalSplitCount;
			public int DirectionalTileSize;
			public TextureHandle DirectionalShadowMap;

			public int PunctualTileCount;
			public int PunctualSplitCount;
			public int PunctualTileSize;
			public BufferHandle PunctualShadowTileBufferHandle;
			public TextureHandle PunctualShadowMap;
		}

		public void RecordRenderGraph(ref PassParams passParams)
		{
			if (!passParams.DrawShadow || _directionalShadowCount + _punctualTileCount == 0) return;

			RenderGraph renderGraph = passParams.RenderGraph;
			CullingResults cullingResults = passParams.CullingResults;
			ShadowSettings settings = passParams.CommonSettings.ShadowSettings;

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);
			passData.ShadowPasses = this;
			passData.Settings = settings;

			//タイルの枚数に応じて、シャドウマップを縦横に何分割するか決める。
			int cascadeCount = settings.CascadeCount;
			int directinalTileCount = _directionalShadowCount * cascadeCount;
			passData.DirectionalSplitCount = MapSplitCount(directinalTileCount);
			passData.DirectionalTileSize = (int)(settings.DirectionalShadowMapSize / (float)passData.DirectionalSplitCount);
			passData.DirectionalShadowCount = _directionalShadowCount;
			float directionalMapSizeRcp = 1.0f / settings.DirectionalShadowMapSize;

			for (int i = 0; i < _directionalShadowCount; i++)
			{
				DirectionalShadowData shadowData = _directionalShadowData[i];
				ShadowDrawingSettings shadowDrawingSettings = new(cullingResults, shadowData.LightIndex)
				{
					//useRenderingLayerMaskTest = true,
				};

				int lightIndex = shadowData.LightIndex;
				int splitOffset = lightIndex * MAX_TILE_COUNT_PER_LIGHT;//cullingで用いる値。最大消費タイル数（point light）でstrideする。

				//ライト一つにつきcascadeの個数ぶんの処理する。つまり一回のDrawごとに必要な準備の処理ということになる。
				for (int j = 0; j < cascadeCount; j++)
				{
					int tileIndex = i * cascadeCount + j;

					cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
						activeLightIndex: lightIndex,
						splitIndex: j,
						splitCount: cascadeCount,
						splitRatio: settings.CascadeRatios,
						passData.DirectionalTileSize,
						shadowData.Light.shadowNearPlane,
						out Matrix4x4 view,
						out Matrix4x4 projection,
						out ShadowSplitData splitData
						);

					Vector2 offset = new Vector2(tileIndex % passData.DirectionalSplitCount, tileIndex / passData.DirectionalSplitCount) * passData.DirectionalTileSize;

					Matrix4x4 worldToShadow = GetShadowTransform(projection, view, offset, directionalMapSizeRcp, passData.DirectionalTileSize);

					Rect viewport = new(offset.x, offset.y, passData.DirectionalTileSize, passData.DirectionalTileSize);
					_worldToDirectionalShadows[tileIndex] = worldToShadow;

					//splitData.shadowCascadeBlendCullingFactor = 0f;

					//culling sphereはライトに関わらず一定なので、最初の一回だけでよい。
					if (i == 0) _cullingSpheres[j] = splitData.cullingSphere;

					//RendererListの取得。
					RendererListHandle rendererList = renderGraph.CreateShadowRendererList(ref shadowDrawingSettings);
					builder.UseRendererList(rendererList);

					//描画一回ごとに必要なデータ。
					shadowData.PerRenderData[j] = new PerRenderData
					{
						RendererList = rendererList,
						View = view,
						Projection = projection,
						SplitData = splitData,
						Viewport = viewport,
					};

					_splitBuffer[splitOffset + j] = splitData;
				}

				//ライト1つごとに必要なデータ。カリングのために使う。
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
				clearBuffer = true,
			});
			passData.DirectionalShadowMap = passParams.LightingResources.DirectionalShadowMap;
			builder.UseTexture(passData.DirectionalShadowMap, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(passData.DirectionalShadowMap, IdDirectionalShadowMap);
		


			//タイルの枚数に応じて、シャドウマップを縦横に何分割するか決める。
			passData.PunctualSplitCount = MapSplitCount(_punctualTileCount);
			passData.PunctualTileSize = (int)(settings.PunctualShadowMapSize / (float)passData.PunctualSplitCount);
			passData.PunctualTileCount = _punctualTileCount;
			float punctualTileScale = 1f / passData.PunctualSplitCount;
			float punctualMapSizeRcp = 1.0f / settings.PunctualShadowMapSize;

			for (int i = 0; i < _punctualTileCount;)
			{
				PunctualShadowData shadowData = _punctualShadowData[i];
				int lightIndex = shadowData.LightIndex;
				int splitOffset = lightIndex * MAX_TILE_COUNT_PER_LIGHT;
				int tileConsumeCount = PunctualTileConsumeCount(shadowData.Light.type);

				ShadowDrawingSettings shadowDrawingSettings = new(cullingResults, lightIndex)
				{
					//useRenderingLayerMaskTest = true,
				};

				if (shadowData.Light.type == LightType.Spot)
				{
					cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(lightIndex, out Matrix4x4 view, out Matrix4x4 projection, out ShadowSplitData splitData);
					_splitBuffer[splitOffset] = splitData;

					Vector2Int tileIndexOffset = new(i % passData.PunctualSplitCount, i / passData.PunctualSplitCount);
					Vector2 tileOffset = tileIndexOffset * passData.PunctualTileSize;

					float texelSize = 2f / (passData.PunctualTileSize * projection.m00);
					float bias = shadowData.Light.shadowNormalBias * texelSize * 1.4142136f;

					Matrix4x4 worldToShadow = GetShadowTransform(projection, view, tileOffset, punctualMapSizeRcp, passData.PunctualTileSize);

					_punctualShadowTileBuffer[i] = new(tileIndexOffset, punctualTileScale, bias, 1f / settings.PunctualShadowMapSize * 0.5f, worldToShadow);
					RendererListHandle rendererList = renderGraph.CreateShadowRendererList(ref shadowDrawingSettings);
					builder.UseRendererList(rendererList);
					shadowData.PerRenderData[0] = new PerRenderData
					{
						RendererList = rendererList,
						View = view,
						Projection = projection,
						SplitData = splitData,
						Viewport = new(tileOffset.x, tileOffset.y, passData.PunctualTileSize, passData.PunctualTileSize),
					};
				}
				else if (shadowData.Light.type == LightType.Point)
				{
					float texelSize = 2f / passData.PunctualTileSize;
					float bias = shadowData.Light.shadowNormalBias * texelSize * 1.4142136f;
					float fovBias = Mathf.Atan(1f + bias + texelSize) * Mathf.Rad2Deg * 2f - 90f;

					for (int j = 0; j < tileConsumeCount; j++)
					{
						cullingResults.ComputePointShadowMatricesAndCullingPrimitives(lightIndex, (CubemapFace)j, fovBias, out Matrix4x4 view, out Matrix4x4 projection, out ShadowSplitData splitData);
						//影が浮いてしまうのを防ぐ。
						view.m11 = -view.m11;
						view.m12 = -view.m12;
						view.m13 = -view.m13;
						_splitBuffer[splitOffset + j] = splitData;

						int tileIndex = i + j;
						Vector2Int tileIndexOffset = new(tileIndex % passData.PunctualSplitCount, tileIndex / passData.PunctualSplitCount);
						Vector2 tileOffset = tileIndexOffset * passData.PunctualTileSize;
						Matrix4x4 worldToShadow = GetShadowTransform(projection, view, tileOffset, punctualMapSizeRcp, passData.PunctualTileSize);

						_punctualShadowTileBuffer[tileIndex] = new(tileIndexOffset, punctualTileScale, bias, 1f / settings.PunctualShadowMapSize * 0.5f, worldToShadow);

						RendererListHandle rendererList = renderGraph.CreateShadowRendererList(ref shadowDrawingSettings);
						builder.UseRendererList(rendererList);
						shadowData.PerRenderData[j] = new PerRenderData
						{
							RendererList = rendererList,
							View = view,
							Projection = projection,
							SplitData = splitData,
							Viewport = new(tileOffset.x, tileOffset.y, passData.PunctualTileSize, passData.PunctualTileSize),
						};
					}
				}

				//ライト1つごとに必要なデータ。カリングのために使う。
				_perLightInfos[lightIndex] = new LightShadowCasterCullingInfo
				{
					projectionType = BatchCullingProjectionType.Perspective,
					splitRange = new RangeInt(splitOffset, tileConsumeCount) // start, length
				};

				i += tileConsumeCount;
			}

			//最大数で確保しておくことでRenderGraph内でうまくプーリングしてくれるらしい。
			passData.PunctualShadowTileBufferHandle = renderGraph.CreateBuffer(new BufferDesc(MAX_PUNCTUAL_TILE_COUNT, PunctualShadowTileBuffer.STRIDE)
			{
				name = "Punctual Shadow Tile Buffer"
			});
			builder.UseBuffer(passData.PunctualShadowTileBufferHandle, AccessFlags.Write);

			passParams.LightingResources.PunctualShadowMap = renderGraph.CreateTexture(new TextureDesc(settings.PunctualShadowMapSize, settings.PunctualShadowMapSize)
			{
				depthBufferBits = DepthBits.Depth32,
				isShadowMap = true,
				name = "Punctual Shadow Map",
				clearBuffer = true,
			});
			passData.PunctualShadowMap = passParams.LightingResources.PunctualShadowMap;
			builder.UseTexture(passData.PunctualShadowMap, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(passData.PunctualShadowMap, IdPunctualShadowMap);

			//shadow casterのカリング。
			passParams.RenderContext.CullShadowCasters(passParams.CullingResults, new ShadowCastersCullingInfos
			{
				splitBuffer = _splitBuffer,
				perLightInfos = _perLightInfos,
			});

			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				IUnsafeCommandBuffer cmd = context.cmd;
				ShadowPasses shadow = passData.ShadowPasses;
				ShadowSettings settings = passData.Settings;

				cmd.SetGlobalMatrixArray(IdWorldToDirectionalShadows, shadow._worldToDirectionalShadows);
				cmd.SetRenderTarget(passData.DirectionalShadowMap);

				for (int i = 0; i < passData.DirectionalShadowCount; i++)
				{
					DirectionalShadowData shadowData = shadow._directionalShadowData[i];

					for (int j = 0; j < settings.CascadeCount; j++)
					{
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
						cmd.SetGlobalDepthBias(0, shadowData.Light.shadowBias);

						// ShadowMapは複数枚をタイル状に詰めて使うので、それぞれのViewportを設定する。左下から順に詰めていく。
						cmd.SetViewport(perRenderData.Viewport);
						cmd.SetViewProjectionMatrices(perRenderData.View, perRenderData.Projection);
						cmd.DrawRendererList(perRenderData.RendererList);
					}
				}

				cmd.SetRenderTarget(passData.PunctualShadowMap);

				for (int i = 0; i < shadow._punctualTileCount;)
				{
					PunctualShadowData shadowData = shadow._punctualShadowData[i];
					int tileConsumeCount = PunctualTileConsumeCount(shadowData.Light.type);

					if (shadowData.Light.type == LightType.Spot)
					{
						PerRenderData perRenderData = shadowData.PerRenderData[0];
						cmd.SetViewport(perRenderData.Viewport);
						cmd.SetViewProjectionMatrices(perRenderData.View, perRenderData.Projection);
						cmd.DrawRendererList(perRenderData.RendererList);
					}
					else if (shadowData.Light.type == LightType.Point)
					{
						for (int j = 0; j < tileConsumeCount; j++)
						{
							PerRenderData perRenderData = shadowData.PerRenderData[j];
							cmd.SetViewport(perRenderData.Viewport);
							cmd.SetViewProjectionMatrices(perRenderData.View, perRenderData.Projection);
							cmd.DrawRendererList(perRenderData.RendererList);
						}
					}
					i += tileConsumeCount;
				}
				cmd.SetBufferData(passData.PunctualShadowTileBufferHandle, shadow._punctualShadowTileBuffer, 0, 0, shadow._punctualTileCount);
				cmd.SetGlobalBuffer(IdPunctualShadowTileBuffer, passData.PunctualShadowTileBufferHandle);
			});
		}
	}
}