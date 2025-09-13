#ifndef TRP_FORWARD_PLUS
#define TRP_FORWARD_PLUS

#include "LitInput.hlsl"

//xy: Screen UV to tile coordinates
//z: 一行あたりのタイルの個数
//w: タイルデータのサイズ
//float4 _ForwardPlusTileSettings;

//CatlikeCodingのロジックを参考に。
struct ForwardPlusTile
{
	int2 coordinates;

	int index;
	
	int GetDataCountPerTile()
	{
		return round(_ForwardPlusTileSettings.w);
	}

	int GetHeaderIndex()
	{
		return index * GetDataCountPerTile();
	}

	int GetLightCount()
	{
		return _TileBuffer[GetHeaderIndex()];
	}

	int GetFirstLightIndexInTile()
	{
		return GetHeaderIndex() + 1;
	}

	int GetLastLightIndexInTile()
	{
		return GetHeaderIndex() + GetLightCount();
	}

	int GetLightIndex(int lightIndexInTile)
	{
		return _TileBuffer[lightIndexInTile];
	}

	bool IsMinimumEdgePixel(float2 screenUv)
	{
		float2 startUv = coordinates / _ForwardPlusTileSettings.xy;
		return any(screenUv - startUv < _AttachmentSize.xy);
	}

	int GetMaxLightsPerTile()
	{
		return GetDataCountPerTile() - 1;
	}

	int2 GetScreenSize()
	{
		return int2(round(_AttachmentSize.zw / _ForwardPlusTileSettings.xy));
	}
};

ForwardPlusTile GetForwardPlusTile(float2 screenUv)
{
	ForwardPlusTile tile;
	tile.coordinates = int2(screenUv * _ForwardPlusTileSettings.xy);
	tile.index = tile.coordinates.y * round(_ForwardPlusTileSettings.z) + tile.coordinates.x;
	return tile;
}

#endif