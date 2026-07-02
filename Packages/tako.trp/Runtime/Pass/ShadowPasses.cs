using System;
using System.Runtime.InteropServices;
using TakoLib.Common;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
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

	[StructLayout(LayoutKind.Sequential)]
	internal struct PunctualShadowTileBuffer
	{
		public const int STRIDE = Defines.SizeOf.FLOAT4 + Defines.SizeOf.FLOAT4X4;
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

	internal sealed class ShadowPasses
	{
		private static readonly ProfilingSampler DirectionalSampler = new("Directional Shadows");
		private static readonly ProfilingSampler PunctualSampler = new("Punctual Shadows");

		private const int MAX_DIRECTIONAL_SHADOW_COUNT = 4;
		private const int MAX_CASCADE_COUNT = 4;
		private const int MAX_PUNCTUAL_TILE_COUNT = 25;
		private const int POINT_TILE_CONSUME_COUNT = 6;
		private const int MAX_TILE_COUNT_PER_LIGHT = POINT_TILE_CONSUME_COUNT;

		private int _directionalShadowCount;
		private int _punctualTileCount;

		private static readonly int IdDirectionalShadowMap = Shader.PropertyToID("_DirectionalShadowMap");
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

		private readonly Matrix4x4[] _worldToDirectionalShadows = new Matrix4x4[MAX_DIRECTIONAL_SHADOW_COUNT * MAX_CASCADE_COUNT];
		private readonly Vector4[] _cullingSpheres = new Vector4[MAX_CASCADE_COUNT];
		private readonly PunctualShadowTileBuffer[] _punctualShadowTileBuffer = new PunctualShadowTileBuffer[MAX_PUNCTUAL_TILE_COUNT];
		private readonly PunctualShadowData[] _punctualShadowData = new PunctualShadowData[MAX_PUNCTUAL_TILE_COUNT];
		private readonly DirectionalShadowData[] _directionalShadowData = new DirectionalShadowData[MAX_DIRECTIONAL_SHADOW_COUNT];
		private readonly PerRenderData[] _directionalPerRenderData = new PerRenderData[MAX_DIRECTIONAL_SHADOW_COUNT * MAX_CASCADE_COUNT];
		private readonly PerRenderData[] _punctualPerRenderData = new PerRenderData[MAX_PUNCTUAL_TILE_COUNT];

		private NativeArray<ShadowSplitData> _splitBuffer;
		private NativeArray<LightShadowCasterCullingInfo> _perLightInfos;

		private struct DirectionalShadowData
		{
			public Light Light;
			public int LightIndex;

			public DirectionalShadowData(Light light, int index)
			{
				Light = light;
				LightIndex = index;
			}
		}

		private struct PunctualShadowData
		{
			public Light Light;
			public int LightIndex;

			public PunctualShadowData(Light light, int index)
			{
				Light = light;
				LightIndex = index;
			}
		}

		private struct PerRenderData
		{
			public RendererListHandle RendererList;
			public Matrix4x4 View;
			public Matrix4x4 Projection;
			public Rect Viewport;
		}

		private sealed class DirectionalPassData
		{
			public ShadowPasses Shadows;
			public ShadowSettings Settings;
			public int ShadowCount;
			public int CascadeCount;
			public TextureHandle ShadowMap;
		}

		private sealed class PunctualPassData
		{
			public ShadowPasses Shadows;
			public int TileCount;
			public TextureHandle ShadowMap;
			public BufferHandle TileBuffer;
		}

		private static int PunctualTileConsumeCount(LightType type)
		{
			return type switch
			{
				LightType.Spot => 1,
				LightType.Point => POINT_TILE_CONSUME_COUNT,
				_ => throw new ArgumentException(),
			};
		}

		private static int MapSplitCount(int tileCount)
		{
			return tileCount switch
			{
				<= 1 => 1,
				<= 4 => 2,
				<= 16 => 4,
				<= 25 => 5,
				_ => throw new ArgumentException(),
			};
		}

		public void Setup(CullingResults cullingResults)
		{
			_directionalShadowCount = 0;
			_punctualTileCount = 0;
			_perLightInfos = new NativeArray<LightShadowCasterCullingInfo>(cullingResults.visibleLights.Length, Allocator.Temp);
			_splitBuffer = new NativeArray<ShadowSplitData>(cullingResults.visibleLights.Length * MAX_TILE_COUNT_PER_LIGHT, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		}

		public PerLightShadowData RegisterDirectionalShadow(Light light, int index, CullingResults cullingResults, ShadowSettings settings)
		{
			if (_directionalShadowCount >= MAX_DIRECTIONAL_SHADOW_COUNT ||
			    light.shadows == LightShadows.None ||
			    light.shadowStrength <= 0.00001f ||
			    !cullingResults.GetShadowCasterBounds(index, out _)) return PerLightShadowData.Empty;

			_directionalShadowData[_directionalShadowCount] = new DirectionalShadowData(light, index);
			int mapTileStartIndex = _directionalShadowCount * settings.CascadeCount;
			_directionalShadowCount++;
			return new PerLightShadowData(LightType.Directional, light.shadowStrength, mapTileStartIndex, light.shadowNormalBias);
		}

		public PerLightShadowData RegisterPunctualShadow(Light light, int index, CullingResults cullingResults, ShadowSettings settings)
		{
			int afterTileCount = _punctualTileCount + PunctualTileConsumeCount(light.type);
			if (afterTileCount > MAX_PUNCTUAL_TILE_COUNT ||
			    light.shadows == LightShadows.None ||
			    light.shadowStrength <= 0.00001f ||
			    !cullingResults.GetShadowCasterBounds(index, out _)) return PerLightShadowData.Empty;

			_punctualShadowData[_punctualTileCount] = new PunctualShadowData(light, index);
			PerLightShadowData result = new(light.type, light.shadowStrength, _punctualTileCount, light.shadowNormalBias);
			_punctualTileCount = afterTileCount;
			return result;
		}

		private static Matrix4x4 GetShadowTransform(Matrix4x4 projection, Matrix4x4 view, Vector2 offset, float mapSizeRcp, float tileSize)
		{
			if (SystemInfo.usesReversedZBuffer)
			{
				projection.m20 = -projection.m20;
				projection.m21 = -projection.m21;
				projection.m22 = -projection.m22;
				projection.m23 = -projection.m23;
			}

			Matrix4x4 worldToShadow = projection * view;
			Matrix4x4 textureScaleAndBias = Matrix4x4.identity;
			textureScaleAndBias.m00 = 0.5f;
			textureScaleAndBias.m11 = 0.5f;
			textureScaleAndBias.m22 = 0.5f;
			textureScaleAndBias.m03 = 0.5f;
			textureScaleAndBias.m13 = 0.5f;
			textureScaleAndBias.m23 = 0.5f;
			worldToShadow = textureScaleAndBias * worldToShadow;

			Matrix4x4 mapToTile = Matrix4x4.identity;
			mapToTile.m00 = tileSize * mapSizeRcp;
			mapToTile.m11 = tileSize * mapSizeRcp;
			mapToTile.m03 = offset.x * mapSizeRcp;
			mapToTile.m13 = offset.y * mapSizeRcp;
			return mapToTile * worldToShadow;
		}

		public void RecordRenderGraph(ref PassParams passParams)
		{
			if (!passParams.DrawShadow || _directionalShadowCount + _punctualTileCount == 0) return;

			if (_directionalShadowCount > 0) RecordDirectionalShadows(ref passParams);
			if (_punctualTileCount > 0) RecordPunctualShadows(ref passParams);

			passParams.RenderContext.CullShadowCasters(passParams.CullingResults, new ShadowCastersCullingInfos
			{
				splitBuffer = _splitBuffer,
				perLightInfos = _perLightInfos,
			});
		}

		private void RecordDirectionalShadows(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			CullingResults cullingResults = passParams.CullingResults;
			ShadowSettings settings = passParams.CommonSettings.ShadowSettings;
			int cascadeCount = settings.CascadeCount;
			int tileCount = _directionalShadowCount * cascadeCount;
			int splitCount = MapSplitCount(tileCount);
			int tileSize = settings.DirectionalShadowMapSize / splitCount;
			float mapSizeRcp = 1f / settings.DirectionalShadowMapSize;

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(DirectionalSampler.name, out DirectionalPassData passData, DirectionalSampler);
			passData.Shadows = this;
			passData.Settings = settings;
			passData.ShadowCount = _directionalShadowCount;
			passData.CascadeCount = cascadeCount;

			for (int i = 0; i < _directionalShadowCount; i++)
			{
				DirectionalShadowData shadowData = _directionalShadowData[i];
				ShadowDrawingSettings drawingSettings = new(cullingResults, shadowData.LightIndex);
				int splitOffset = shadowData.LightIndex * MAX_TILE_COUNT_PER_LIGHT;

				for (int cascade = 0; cascade < cascadeCount; cascade++)
				{
					int tileIndex = i * cascadeCount + cascade;
					cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
						shadowData.LightIndex, cascade, cascadeCount, settings.CascadeRatios, tileSize,
						shadowData.Light.shadowNearPlane, out Matrix4x4 view, out Matrix4x4 projection, out ShadowSplitData splitData);

					Vector2 offset = new Vector2(tileIndex % splitCount, tileIndex / splitCount) * tileSize;
					_worldToDirectionalShadows[tileIndex] = GetShadowTransform(projection, view, offset, mapSizeRcp, tileSize);
					if (i == 0) _cullingSpheres[cascade] = splitData.cullingSphere;

					RendererListHandle rendererList = renderGraph.CreateShadowRendererList(ref drawingSettings);
					builder.UseRendererList(rendererList);
					_directionalPerRenderData[tileIndex] = new PerRenderData
					{
						RendererList = rendererList,
						View = view,
						Projection = projection,
						Viewport = new Rect(offset.x, offset.y, tileSize, tileSize),
					};
					_splitBuffer[splitOffset + cascade] = splitData;
				}

				_perLightInfos[shadowData.LightIndex] = new LightShadowCasterCullingInfo
				{
					projectionType = BatchCullingProjectionType.Orthographic,
					splitRange = new RangeInt(splitOffset, cascadeCount),
				};
			}

			passParams.LightingResources.DirectionalShadowMap = renderGraph.CreateTexture(new TextureDesc(settings.DirectionalShadowMapSize, settings.DirectionalShadowMapSize)
			{
				depthBufferBits = DepthBits.Depth32,
				isShadowMap = true,
				name = "Directional Shadow Map",
				clearBuffer = true,
			});
			passData.ShadowMap = passParams.LightingResources.DirectionalShadowMap;
			builder.UseTexture(passData.ShadowMap, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(passData.ShadowMap, IdDirectionalShadowMap);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<DirectionalPassData>(static (data, context) =>
			{
				IUnsafeCommandBuffer cmd = context.cmd;
				ShadowPasses shadows = data.Shadows;
				cmd.SetRenderTarget(data.ShadowMap);

				for (int i = 0; i < data.ShadowCount; i++)
				{
					DirectionalShadowData shadowData = shadows._directionalShadowData[i];
					cmd.SetGlobalDepthBias(0, shadowData.Light.shadowBias);
					for (int cascade = 0; cascade < data.CascadeCount; cascade++)
					{
						PerRenderData renderData = shadows._directionalPerRenderData[i * data.CascadeCount + cascade];
						cmd.SetViewport(renderData.Viewport);
						cmd.SetViewProjectionMatrices(renderData.View, renderData.Projection);
						cmd.DrawRendererList(renderData.RendererList);
					}
				}
				cmd.SetGlobalDepthBias(0, 0);

				cmd.SetGlobalMatrixArray(IdWorldToDirectionalShadows, shadows._worldToDirectionalShadows);
				cmd.SetGlobalVector(IdCullingSphere0, shadows._cullingSpheres[0]);
				cmd.SetGlobalVector(IdCullingSphere1, shadows._cullingSpheres[1]);
				cmd.SetGlobalVector(IdCullingSphere2, shadows._cullingSpheres[2]);
				cmd.SetGlobalVector(IdCullingSphere3, shadows._cullingSpheres[3]);

				Vector4 radiusSq = new(
					shadows._cullingSpheres[0].w * shadows._cullingSpheres[0].w,
					shadows._cullingSpheres[1].w * shadows._cullingSpheres[1].w,
					shadows._cullingSpheres[2].w * shadows._cullingSpheres[2].w,
					shadows._cullingSpheres[3].w * shadows._cullingSpheres[3].w);
				cmd.SetGlobalVector(IdCullingSphereRadiusSqrs, radiusSq);
				cmd.SetGlobalVector(IdCullingSphereRanges, new Vector4(
					SafeRcp(radiusSq.x), SafeRcp(radiusSq.y - radiusSq.x),
					SafeRcp(radiusSq.z - radiusSq.y), SafeRcp(radiusSq.w - radiusSq.z)));
				cmd.SetGlobalVector(IdShadowParams1, new Vector4(
					-5f * data.Settings.CascadeFade,
					data.Settings.DistanceFade,
					data.Settings.MaxShadowDistance * data.Settings.MaxShadowDistance,
					data.CascadeCount));
			});
		}

		private void RecordPunctualShadows(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			CullingResults cullingResults = passParams.CullingResults;
			ShadowSettings settings = passParams.CommonSettings.ShadowSettings;
			int splitCount = MapSplitCount(_punctualTileCount);
			int tileSize = settings.PunctualShadowMapSize / splitCount;
			float tileScale = 1f / splitCount;
			float mapSizeRcp = 1f / settings.PunctualShadowMapSize;

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(PunctualSampler.name, out PunctualPassData passData, PunctualSampler);
			passData.Shadows = this;
			passData.TileCount = _punctualTileCount;

			for (int i = 0; i < _punctualTileCount;)
			{
				PunctualShadowData shadowData = _punctualShadowData[i];
				int lightIndex = shadowData.LightIndex;
				int splitOffset = lightIndex * MAX_TILE_COUNT_PER_LIGHT;
				int consumeCount = PunctualTileConsumeCount(shadowData.Light.type);
				ShadowDrawingSettings drawingSettings = new(cullingResults, lightIndex);

				if (shadowData.Light.type == LightType.Spot)
				{
					cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(lightIndex, out Matrix4x4 view, out Matrix4x4 projection, out ShadowSplitData splitData);
					_splitBuffer[splitOffset] = splitData;
					WritePunctualTile(renderGraph, builder, i, splitCount, tileSize, tileScale, mapSizeRcp,
						shadowData.Light, ref drawingSettings, view, projection, splitData);
				}
				else
				{
					float texelSize = 2f / tileSize;
					float bias = shadowData.Light.shadowNormalBias * texelSize * 1.4142136f;
					float fovBias = Mathf.Atan(1f + bias + texelSize) * Mathf.Rad2Deg * 2f - 90f;
					for (int face = 0; face < consumeCount; face++)
					{
						cullingResults.ComputePointShadowMatricesAndCullingPrimitives(lightIndex, (CubemapFace)face, fovBias,
							out Matrix4x4 view, out Matrix4x4 projection, out ShadowSplitData splitData);
						view.m11 = -view.m11;
						view.m12 = -view.m12;
						view.m13 = -view.m13;
						_splitBuffer[splitOffset + face] = splitData;
						WritePunctualTile(renderGraph, builder, i + face, splitCount, tileSize, tileScale, mapSizeRcp,
							shadowData.Light, ref drawingSettings, view, projection, splitData);
					}
				}

				_perLightInfos[lightIndex] = new LightShadowCasterCullingInfo
				{
					projectionType = BatchCullingProjectionType.Perspective,
					splitRange = new RangeInt(splitOffset, consumeCount),
				};
				i += consumeCount;
			}

			passData.TileBuffer = renderGraph.CreateBuffer(new BufferDesc(MAX_PUNCTUAL_TILE_COUNT, PunctualShadowTileBuffer.STRIDE)
			{
				name = "Punctual Shadow Tile Buffer",
			});
			passParams.LightingResources.PunctualShadowDataBuffer = passData.TileBuffer;
			builder.UseBuffer(passData.TileBuffer, AccessFlags.Write);

			passParams.LightingResources.PunctualShadowMap = renderGraph.CreateTexture(new TextureDesc(settings.PunctualShadowMapSize, settings.PunctualShadowMapSize)
			{
				depthBufferBits = DepthBits.Depth32,
				isShadowMap = true,
				name = "Punctual Shadow Map",
				clearBuffer = true,
			});
			passData.ShadowMap = passParams.LightingResources.PunctualShadowMap;
			builder.UseTexture(passData.ShadowMap, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(passData.ShadowMap, IdPunctualShadowMap);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<PunctualPassData>(static (data, context) =>
			{
				IUnsafeCommandBuffer cmd = context.cmd;
				ShadowPasses shadows = data.Shadows;
				cmd.SetRenderTarget(data.ShadowMap);

				for (int i = 0; i < data.TileCount;)
				{
					PunctualShadowData shadowData = shadows._punctualShadowData[i];
					int consumeCount = PunctualTileConsumeCount(shadowData.Light.type);
					cmd.SetGlobalDepthBias(0, shadowData.Light.shadowBias);
					for (int tile = 0; tile < consumeCount; tile++)
					{
						PerRenderData renderData = shadows._punctualPerRenderData[i + tile];
						cmd.SetViewport(renderData.Viewport);
						cmd.SetViewProjectionMatrices(renderData.View, renderData.Projection);
						cmd.DrawRendererList(renderData.RendererList);
					}
					i += consumeCount;
				}

				cmd.SetGlobalDepthBias(0, 0);
				cmd.SetBufferData(data.TileBuffer, shadows._punctualShadowTileBuffer, 0, 0, data.TileCount);
				cmd.SetGlobalBuffer(IdPunctualShadowTileBuffer, data.TileBuffer);
			});
		}

		private void WritePunctualTile(
			RenderGraph renderGraph,
			IUnsafeRenderGraphBuilder builder,
			int tileIndex,
			int splitCount,
			int tileSize,
			float tileScale,
			float mapSizeRcp,
			Light light,
			ref ShadowDrawingSettings drawingSettings,
			Matrix4x4 view,
			Matrix4x4 projection,
			ShadowSplitData splitData)
		{
			Vector2Int tileIndexOffset = new(tileIndex % splitCount, tileIndex / splitCount);
			Vector2 tileOffset = tileIndexOffset * tileSize;
			float texelSize = light.type == LightType.Spot ? 2f / (tileSize * projection.m00) : 2f / tileSize;
			float bias = light.shadowNormalBias * texelSize * 1.4142136f;
			Matrix4x4 worldToShadow = GetShadowTransform(projection, view, tileOffset, mapSizeRcp, tileSize);
			_punctualShadowTileBuffer[tileIndex] = new PunctualShadowTileBuffer(
				tileIndexOffset, tileScale, bias, mapSizeRcp * 0.5f, worldToShadow);

			RendererListHandle rendererList = renderGraph.CreateShadowRendererList(ref drawingSettings);
			builder.UseRendererList(rendererList);
			_punctualPerRenderData[tileIndex] = new PerRenderData
			{
				RendererList = rendererList,
				View = view,
				Projection = projection,
				Viewport = new Rect(tileOffset.x, tileOffset.y, tileSize, tileSize),
			};
		}

		private static float SafeRcp(float value) => Mathf.Abs(value) > 1e-6f ? 1f / value : 0f;
	}
}
