using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using Unity.Collections;
using Unity.Mathematics;
using TakoLib.Common.Extensions;
using Unity.Jobs;
using System;
using System.Runtime.InteropServices;

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

	internal class LightingForwardPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.LightingForward);

		private static readonly int IdDirectionalLightCount = Shader.PropertyToID("_DirectionalLightCount");
		private static readonly int IdDirectionalLightData1 = Shader.PropertyToID("_DirectionalLightData1");
		private static readonly int IdDirectionalLightData2 = Shader.PropertyToID("_DirectionalLightData2");

		private static readonly int IdPunctualLightCount = Shader.PropertyToID("_PunctualLightCount");
		private static readonly int IdPunctualLightBuffer = Shader.PropertyToID("_PunctualLightBuffer");
		private static readonly int IdTileBuffer = Shader.PropertyToID("_TileBuffer");
		private static readonly int IdFowardPlusTileSettings = Shader.PropertyToID("_ForwardPlusTileSettings");

		private const int MAX_DIRECTIONAL_LIGHT_COUNT = 4;
		private const int MAX_PUNCTUAL_LIGHT_COUNT = 64;

		private NativeArray<float4> _lightBounds;
		private NativeArray<int> _tileData;


		[StructLayout(LayoutKind.Sequential)]
		public struct PunctualLightData
		{
			//Vector4のサイズ（= float（4バイト） x 4つ） x 4つ（変数の個数）。
			public static readonly int Stride = 4 * 4 * 4;

			public float4 Data1, Data2, Data3, Data4;
			public PunctualLightData(ref VisibleLight visibleLight)
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
					float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.light.innerSpotAngle);
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
			public TextureHandle Src;
			public Camera Camera;
			public JobHandle TileJobHandle;
			public int TileCount;
			public NativeArray<int> TileData;
			public BufferHandle TileBuffer;
			public NativeArray<float4> LightBounds;
			public Vector4 FowardPlusTileSettings;

			public int DirectionalLightCount;
			public readonly Vector4[] DirectionalLightData1 = new Vector4[MAX_DIRECTIONAL_LIGHT_COUNT];
			public readonly Vector4[] DirectionalLightData2 = new Vector4[MAX_DIRECTIONAL_LIGHT_COUNT];

			public int PunctualLightCount;
			public readonly PunctualLightData[] PunctualLightData = new PunctualLightData[MAX_PUNCTUAL_LIGHT_COUNT];
			public BufferHandle PunctualLightBuffer;
		}

		internal void RecordRenderGraph(ref PassParams passParams, LightingForwardPlusSettings lightingSettings)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(Sampler.name, out PassData passData, Sampler);

			NativeArray<VisibleLight> visibleLights = passParams.CullingResults.visibleLights;
			_lightBounds = new(MAX_PUNCTUAL_LIGHT_COUNT, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

			int directionalLightCount = 0;
			int punctualLightCount = 0;

			//TODO: ライト最大数の反映
			for (int i = 0; i < visibleLights.Length; i++)
			{
				VisibleLight visibleLight = visibleLights[i];

				if (visibleLight.lightType == LightType.Directional)
				{
					//xyz: 角度
					passData.DirectionalLightData1[directionalLightCount] = -visibleLight.localToWorldMatrix.GetColumn(2);

					//xyz: 色
					//w: 未使用
					passData.DirectionalLightData2[directionalLightCount] = new float4(visibleLight.finalColor.ToFloat3(), 1);

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

					passData.PunctualLightData[punctualLightCount] = new PunctualLightData(ref visibleLight);

					punctualLightCount++;
				}
			}
			passData.DirectionalLightCount = directionalLightCount;
			passData.PunctualLightCount = punctualLightCount;

			if (passParams.Camera.cameraType == CameraType.Preview)
			{
				passData.DirectionalLightCount = 1;
				passData.DirectionalLightData1[0] = new(1, 0, 0);
				passData.DirectionalLightData2[0] = new(1, 1, 1);
			}

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
				JobHandle tileJobHandle = new ForwardPlusTileJob
				{
					TileData = _tileData,
					LightBounds = _lightBounds,
					LightCount = punctualLightCount,
					LightCountPerTile = lightCountPerTile,
					DataCountPerTile = dataCountPerTile,
					TileCountX = tileCountXY.x,
					TileScreenUvSize = 1f / screenUvToTileCoordinates,
				}
				.ScheduleParallel(tileCountXY.x * tileCountXY.y, tileCountXY.x, default);
				passData.TileJobHandle = tileJobHandle;
				passData.TileCount = tileCount;
				passData.TileData = _tileData;
				passData.LightBounds = _lightBounds;

				//プーリングのため最大数で取得。
				BufferDesc tileBufferDesc = new (tileCount * (lightingSettings.MaxLightCountPerTile + 1), sizeof(int))
				{
					name = "Forward+ Tile Buffer",
				};
				passData.TileBuffer = renderGraph.CreateBuffer(tileBufferDesc);
				passParams.ForwardPlusTileBuffer = passData.TileBuffer;

				//プーリングのため最大数で取得。
				BufferDesc punctualLightBufferDesc = new (MAX_PUNCTUAL_LIGHT_COUNT, PunctualLightData.Stride)
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

				cmd.SetGlobalVectorArray(IdDirectionalLightData1, passData.DirectionalLightData1);
				cmd.SetGlobalVectorArray(IdDirectionalLightData2, passData.DirectionalLightData2);

				cmd.SetGlobalFloat(IdPunctualLightCount, passData.PunctualLightCount);

				if (0 < passData.PunctualLightCount)
				{
					//Jobの時間稼ぎのためここでようやくComplete。
					passData.TileJobHandle.Complete();

					cmd.SetBufferData(passData.PunctualLightBuffer, passData.PunctualLightData, 0, 0, passData.PunctualLightCount);
					cmd.SetGlobalBuffer(IdPunctualLightBuffer, passData.PunctualLightBuffer);

					cmd.SetBufferData(passData.TileBuffer, passData.TileData);
					cmd.SetGlobalBuffer(IdTileBuffer, passData.TileBuffer);

					cmd.SetGlobalVector(IdFowardPlusTileSettings, passData.FowardPlusTileSettings);
					cmd.SetGlobalFloat(IdPunctualLightCount, passData.PunctualLightCount);
				}
				passData.TileData.Dispose();
				passData.LightBounds.Dispose();
			});
		}

		public void Dispose()
		{
			_tileData.Dispose();
			_lightBounds.Dispose();
		}
	}
}