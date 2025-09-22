using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using static Unity.Mathematics.math;
using UnityEngine;

namespace Trp
{
	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
	internal struct ForwardPlusTileJob : IJobFor
	{
		//Header（ライトの個数）とライトの番号（DataCountPerTile個）を交互に詰める配列。
		//例えばDataCountPerTileが5のとき...、
		//3|257**, 0|*****, 0|*****, 2|56***, 5|13479, 4|4678*,...みたいな感じ。（実際はintの配列なので「|」や「,」はない。）
		//シェーダー側では、例えば該当のタイルのHeaderが3なら、そこから次の3つの番号だけを読む。
		//外部で定義された一つのNativeArrayに対してJobで並列に書き込むためNativeDisableParallelForRestrictionが必要。
		[WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> TileData;
		[ReadOnly] public NativeArray<float4> LightBounds;

		public int LightCount;
		public int LightCountPerTile;
		public int DataCountPerTile; // LightCountPerTile + 1。
		public int TileCountX;
		public float2 TileScreenUvSize;

		public void Execute(int tileIndex)
		{
			int y = tileIndex / TileCountX;
			int x = tileIndex - y * TileCountX;
			float4 tileBounds = float4(x, y, x + 1, y + 1) * TileScreenUvSize.xyxy;

			int headerIndex = tileIndex * DataCountPerTile;
			int dataIndex = headerIndex;
			int lightCountInTile = 0;

			//シーンにある全ライトを走査し、タイルに含まれるライトの番号をTileDataに詰める。
			for (int i = 0; i < LightCount; i++)
			{
				float4 lightBounds = LightBounds[i];

				//ライトがタイルに含まれる場合。
				if (all(float4(lightBounds.xy, tileBounds.xy) <= float4(tileBounds.zw, lightBounds.zw)))
				{
					dataIndex++;
					TileData[dataIndex] = i;

					lightCountInTile++;
					//タイルごとのライトの最大数を超過したら切り上げ。
					if (LightCountPerTile <= lightCountInTile) break;
				}
			}
			//タイルにライトがいくつ含まれるのかheaderに登録する。
			TileData[headerIndex] = lightCountInTile;
		}
	}
}