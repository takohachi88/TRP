using System;
using System.Runtime.InteropServices;
using TakoLib.Common.Extensions;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	[Serializable]
	public class LightingForwardPlusSettings
	{
		[Min(1)] public int MaxLightCountPerTile = 16;
		public ForwardPlusTileSize TileSize = ForwardPlusTileSize.Size64;
	}

	public enum ForwardPlusTileSize
	{
		Size32 = 32,
		Size64 = 64,
		Size128 = 128,
	}

	[Serializable]
	public class ShadowSettings
	{
		[Min(0)] public float MaxShadowDistance = 50;
		[Range(1, 4)] public int CascadeCount = 1;
		public Vector3 CascadeRatios;
		[Range(0.001f, 1f)] public float DistanceFade = 0.1f;
		[Range(0.001f, 1f)] public float CascadeFade = 0.3f;
		public enum MapSize
		{
			_1024 = 1024,
			_2048 = 2048,
			_4096 = 4096,
			_8192 = 8192,
		}
		public int DirectionalShadowMapSize = (int)MapSize._2048;
		public int PunctualShadowMapSize = (int)MapSize._2048;
	}

	internal class LightingForwardPlusPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.LightingForward);

		private static readonly int IdDirectionalLightCount = Shader.PropertyToID("_DirectionalLightCount");
		private static readonly int IdDirectionalLightBuffer = Shader.PropertyToID("_DirectionalLightBuffer");

		private static readonly int IdPunctualLightCount = Shader.PropertyToID("_PunctualLightCount");
		private static readonly int IdPunctualLightBuffer = Shader.PropertyToID("_PunctualLightBuffer");
		private static readonly int IdTileBuffer = Shader.PropertyToID("_TileBuffer");
		private static readonly int IdFowardPlusTileSettings = Shader.PropertyToID("_ForwardPlusTileSettings");

		private const int MAX_DIRECTIONAL_LIGHT_COUNT = 4;
		private const int MAX_PUNCTUAL_LIGHT_COUNT = 64;

		private NativeArray<float4> _lightBounds;
		private NativeArray<int> _tileData;

		public JobHandle _tileJobHandle;

		private readonly ShadowPasses _shadowPasses = new();

		[StructLayout(LayoutKind.Sequential)]
		public struct DirectionalLightData
		{
			//このstructが何バイトか？
			public const int STRIDE = sizeof(float) * 4 * 3; //float4のサイズ（= float（4バイト） x 4つ） x 変数の個数

			/// <summary>
			/// xyz: 角度
			/// w: 未使用
			/// </summary>
			public float4 Data1;

			/// <summary>
			/// xyz: 色
			/// w: renderingLayerMask
			/// </summary>
			public float4 Data2;

			/// <summary>
			/// x: 影の強さ
			/// y: シャドウマップのタイル番号
			/// z: normal bias
			/// w: 未使用
			/// </summary>
			public float4 Data3;

			public DirectionalLightData(ref VisibleLight visibleLight, Light light, PerLightShadowData shadowData)
			{
				Data1 = -visibleLight.localToWorldMatrix.GetColumn(2);
				Data2 = visibleLight.finalColor.ToFloat4();
				Data2.w = math.asfloat(light.renderingLayerMask);
				Data3 = new(shadowData.Strength, shadowData.MapTileStartIndex, shadowData.NormalBias, 0);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct PunctualLightData
		{
			//このstructが何バイトか？
			public const int STRIDE = sizeof(float) * 4 * 4;

			public float4 Data1, Data2, Data3, Data4;
			public PunctualLightData(ref VisibleLight visibleLight, Light light)
			{
				if (visibleLight.lightType is not (LightType.Spot or LightType.Point)) throw new ArgumentException();

				//xyz: 色
				//w: 未使用
				Data2 = visibleLight.finalColor.ToFloat4();

				//xyz: 位置
				//w: 範囲（逆2乗）
				float4 data3 = visibleLight.localToWorldMatrix.GetColumn(3);
				data3.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
				Data3 = data3;

				if (visibleLight.lightType == LightType.Point)
				{
					//xyz: 角度
					//w: 未使用
					Data1 = Vector4.zero;

					// xy: スポットライトの絞りを無効化。
					Data4 = new(0, 1, 0, 0);

					return;
				}
				else if (visibleLight.lightType == LightType.Spot)
				{
					//xyz: 角度
					//w: 未使用
					Data1 = -visibleLight.localToWorldMatrix.GetColumn(2);

					// saturate(d * a + b)^2というスポットライトの絞りの式の...、
					//x: a
					//y: b
					float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
					float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
					float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
					Data4 = new(angleRangeInv, -outerCos * angleRangeInv, 0, 0);

					return;
				}

				Data1 = float4.zero;
				Data4 = float4.zero;
			}
		}


		public class PassData
		{
			public LightingForwardPlusPass Pass;
			public TextureHandle Src;
			public Camera Camera;
			public int TileCount;
			public Vector2Int TileCountXY;
			public int DataCountPerTile;
			public BufferHandle TileBuffer;
			public Vector4 FowardPlusTileSettings;

			public int DirectionalLightCount;
			public readonly DirectionalLightData[] DirectionalLightData = new DirectionalLightData[MAX_DIRECTIONAL_LIGHT_COUNT];
			public BufferHandle DirectionalLightBuffer;

			public int PunctualLightCount;
			public readonly PunctualLightData[] PunctualLightData = new PunctualLightData[MAX_PUNCTUAL_LIGHT_COUNT];
			public BufferHandle PunctualLightBuffer;
		}

		internal void RecordRenderGraph(ref PassParams passParams, LightingForwardPlusSettings lightingSettings)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			if(passParams.DrawShadow) _shadowPasses.Setup(passParams.CullingResults);

			using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(Sampler.name, out PassData passData, Sampler);

			passData.Pass = this;

			NativeArray<VisibleLight> visibleLights = passParams.CullingResults.visibleLights;
			_lightBounds = new(MAX_PUNCTUAL_LIGHT_COUNT, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

			int directionalLightCount = 0;
			int punctualLightCount = 0;

			//TODO: ライト最大数の反映
			for (int i = 0; i < visibleLights.Length; i++)
			{
				VisibleLight visibleLight = visibleLights[i];
				Light light = visibleLight.light;

				if (visibleLight.lightType == LightType.Directional)
				{
					PerLightShadowData shadowData = passParams.DrawShadow ?
						_shadowPasses.RegisterDirectionalShadow(visibleLight.light, i, passParams.CullingResults, passParams.CommonSettings.ShadowSettings) :
						PerLightShadowData.Empty;
					passData.DirectionalLightData[directionalLightCount] = new DirectionalLightData(ref visibleLight, light, shadowData);
					directionalLightCount++;
				}
				else if (visibleLight.lightType is (LightType.Spot or LightType.Point))
				{
					if (MAX_PUNCTUAL_LIGHT_COUNT < punctualLightCount) continue;

					//Forward+のロジックのためライトのBoundsをRectで取得する。
					//理想的にはライトの形状（球やコーン）に基づいて計算するのが良いかもしれないがロジックがかなり複雑になる。
					//Jobに渡す都合上Bounds型からfloat4型にする。
					Rect lightBoundsRect = visibleLight.screenRect;
					_lightBounds[punctualLightCount] = math.float4(lightBoundsRect.xMin, lightBoundsRect.yMin, lightBoundsRect.xMax, lightBoundsRect.yMax);

					passData.PunctualLightData[punctualLightCount] = new PunctualLightData(ref visibleLight, light);

					punctualLightCount++;
				}
			}
			passData.DirectionalLightCount = directionalLightCount;
			passData.PunctualLightCount = punctualLightCount;

			if (passParams.Camera.cameraType == CameraType.Preview)
			{
				//previewの場合ライトはないため適当な値をセット。
				passData.DirectionalLightCount = 1;
				passData.DirectionalLightData[0] = new()
				{
					Data1 = new(1, 1, 1, 0),
					Data2 = new(0, 1, 0, 0),
				};
			}

			//プーリングのため最大数で取得。（countが同じだとRenderGraph内でプーリングしてくれる。）
			BufferDesc directionalLightBufferDesc = new(MAX_DIRECTIONAL_LIGHT_COUNT, DirectionalLightData.STRIDE)
			{
				name = "Directional Light Buffer",
			};
			passData.DirectionalLightBuffer = renderGraph.CreateBuffer(directionalLightBufferDesc);
			builder.UseBuffer(passData.DirectionalLightBuffer, AccessFlags.Write);

			if (0 < punctualLightCount)
			{
				//CatlikeCodingのロジックを参考に。
				Vector2 attachmentSize = passParams.AttachmentSize;
				float tileSize = (float)lightingSettings.TileSize;
				float2 screenUvToTileCoordinates = new(attachmentSize.x / tileSize, attachmentSize.y / tileSize);
				Vector2Int tileCountXY = new(Mathf.CeilToInt(screenUvToTileCoordinates.x), Mathf.CeilToInt(screenUvToTileCoordinates.y));
				int tileCount = tileCountXY.x * tileCountXY.y;
				int lightCountPerTile = Mathf.Min(punctualLightCount, lightingSettings.MaxLightCountPerTile);
				//+1なのはHeaderとしてライトの個数を入れるため。"節"1つにつきintがいくつあるか。
				int dataCountPerTile = lightCountPerTile + 1;
				_tileData = new(tileCount * dataCountPerTile, Allocator.TempJob);

				//JobSystemでForwardPlusのタイルの計算。
				_tileJobHandle = new ForwardPlusTileJob
				{
					TileData = _tileData,
					LightBounds = _lightBounds,
					LightCount = punctualLightCount,
					LightCountPerTile = lightCountPerTile,
					DataCountPerTile = dataCountPerTile,
					TileCountX = tileCountXY.x,
					TileScreenUvSize = 1f / screenUvToTileCoordinates,
				}
				.ScheduleParallel(tileCount, tileCountXY.x, default);
				passData.TileCount = tileCount;
				passData.TileCountXY = tileCountXY;
				passData.DataCountPerTile = dataCountPerTile;

				//プーリングのため最大数で取得。
				BufferDesc tileBufferDesc = new (tileCount * (lightingSettings.MaxLightCountPerTile + 1), sizeof(int))
				{
					name = "Forward+ Tile Buffer",
				};
				passData.TileBuffer = renderGraph.CreateBuffer(tileBufferDesc);
				passParams.LightingResources.ForwardPlusTileBuffer = passData.TileBuffer;

				//プーリングのため最大数で取得。
				BufferDesc punctualLightBufferDesc = new (MAX_PUNCTUAL_LIGHT_COUNT, PunctualLightData.STRIDE)
				{
					name = "Punctual Light Buffer",
				};
				passData.PunctualLightBuffer = renderGraph.CreateBuffer(punctualLightBufferDesc);
				passData.FowardPlusTileSettings = new float4(screenUvToTileCoordinates, tileCountXY.x, dataCountPerTile);
				builder.UseBuffer(passData.TileBuffer, AccessFlags.Write);
				builder.UseBuffer(passData.PunctualLightBuffer, AccessFlags.Write);
			}

			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				IComputeCommandBuffer cmd = context.cmd;

				cmd.SetGlobalFloat(IdDirectionalLightCount, passData.DirectionalLightCount);
				cmd.SetBufferData(passData.DirectionalLightBuffer, passData.DirectionalLightData, 0, 0, passData.DirectionalLightCount);
				cmd.SetGlobalBuffer(IdDirectionalLightBuffer, passData.DirectionalLightBuffer);

				cmd.SetGlobalFloat(IdPunctualLightCount, passData.PunctualLightCount);

				if (0 < passData.PunctualLightCount)
				{
					//Jobの時間稼ぎのためここでようやくComplete。
					passData.Pass._tileJobHandle.Complete();

					cmd.SetBufferData(passData.PunctualLightBuffer, passData.PunctualLightData, 0, 0, passData.PunctualLightCount);
					cmd.SetGlobalBuffer(IdPunctualLightBuffer, passData.PunctualLightBuffer);

					cmd.SetBufferData(passData.TileBuffer, passData.Pass._tileData);
					cmd.SetGlobalBuffer(IdTileBuffer, passData.TileBuffer);

					cmd.SetGlobalVector(IdFowardPlusTileSettings, passData.FowardPlusTileSettings);
					cmd.SetGlobalFloat(IdPunctualLightCount, passData.PunctualLightCount);
				}
				if(passData.Pass._tileData.IsCreated) passData.Pass._tileData.Dispose();
				passData.Pass._lightBounds.Dispose();
			});

			builder.Dispose();

			//影の描画準備。
			if (passParams.DrawShadow) _shadowPasses.RecordRenderGraph(ref passParams);
		}

		public void Dispose()
		{
			if (_tileData.IsCreated) _tileData.Dispose();
			if (_lightBounds.IsCreated) _lightBounds.Dispose();
		}
	}
}