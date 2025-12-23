using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using TakoLib.Common;

namespace Trp
{
	/// <summary>
	/// LightCookieの描画、データ作成。
	/// </summary>
	public class LightCookiePass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.LightCookie);

		/// <summary>
		/// Cookieのアトラス。
		/// Texture2DAtlas型はSRPライブラリ側にあるクラスで、テクスチャを渡せば自動でアトラスを形成してくれる。
		/// </summary>
		private Texture2DAtlas _cookieAtlas;

		[StructLayout(LayoutKind.Sequential)]
		public struct LightCookieBuffer
		{
			//このstructが何バイトか？
			public const int STRIDE = Defines.SizeOf.FLOAT4X4 + Defines.SizeOf.FLOAT4 + Defines.SizeOf.FLOAT;

			public Matrix4x4 WorldToLight;
			public float4 UvScaleOffset;
			public float WrapMode;
		}
		private LightCookieBuffer[] _data;
		private VisibleLight[] _lights;
		private ComputeBuffer _buffer;

		private static readonly int IdLightCookieAtlas = Shader.PropertyToID("_LightCookieAtlas");
		private static readonly int IdLightCookieBuffer = Shader.PropertyToID("_LightCookieBuffer");


		public LightCookiePass(int atlasSize, int defaultDataCount)
		{
			_cookieAtlas = new(atlasSize, atlasSize, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, FilterMode.Bilinear, false, "Trp Light Cookie Atlas", false);
			_data = new LightCookieBuffer[defaultDataCount];
			_lights = new VisibleLight[defaultDataCount];
			_buffer = new ComputeBuffer(defaultDataCount, LightCookieBuffer.STRIDE);
		}

		public void Dispose()
		{
			_cookieAtlas?.Release();
			_buffer?.Dispose();
		}

		public void Setup()
		{
			for (int i = 0; i < _lights.Length; i++) _lights[i] = default;
		}

		public void RegisterCookie(VisibleLight light, int cookieIndex)
		{
			_lights[cookieIndex] = light;
		}

		private class PassData
		{
			public VisibleLight[] Lights;
			public Texture2DAtlas CookieAtlas;
			public LightCookieBuffer[] Data;
			public ComputeBuffer ComputeBuffer;
			public int CookieCount;
		}

		public void RecordRenderGraph(ref PassParams passParams, int cookieCount)
		{
			if (cookieCount <= 0) return; 

			RenderGraph renderGraph = passParams.RenderGraph;

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Lights = _lights;
			passData.CookieAtlas = _cookieAtlas;
			passData.Data = _data;
			passData.ComputeBuffer = _buffer;
			passData.CookieCount = cookieCount;

			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				Texture2DAtlas cookieAtlas = passData.CookieAtlas;

				for (int i = 0; i < passData.CookieCount; i++)
				{
					VisibleLight light = passData.Lights[i];
					Texture cookie = light.light.cookie;
					int cookieSize = cookie.width; //cookieはLightのインスペクタにより正方形であることが保証されている。

					//cube mapの場合、八面体に展開して使う。
					if (cookie.dimension == TextureDimension.Cube) cookieSize = ComputeOctahedralCookieSize(cookie);

					//アトラスにテクスチャが既にキャッシュされているなら更新（update）。されていないなら新たに領域を確保（allocate）。
					if (cookieAtlas.IsCached(out Vector4 uvScaleOffset, cookie)) cookieAtlas.UpdateTexture(cmd, cookie, ref uvScaleOffset);
					else cookieAtlas.AllocateTexture(cmd, ref uvScaleOffset, cookie, cookie.width, cookie.height);

					//ぎっしり詰まったアトラスなのでbilinearサンプリングの際に隣のテクスチャがにじまないよう、0.5pxぶんUVを縮小する。
					const float SHURINK_PIXEL = 0.5f;
					Vector2 shrinkOffset = Vector2.one * SHURINK_PIXEL / cookieSize;
					float uvShrinkScale = (cookieSize - SHURINK_PIXEL * 2) / cookieSize;
					uvScaleOffset.z += uvScaleOffset.x * shrinkOffset.x;
					uvScaleOffset.w += uvScaleOffset.y * shrinkOffset.y;
					uvScaleOffset.x *= uvShrinkScale;
					uvScaleOffset.y *= uvShrinkScale;

					if (!SystemInfo.graphicsUVStartsAtTop) uvScaleOffset.w = 1.0f - uvScaleOffset.w - uvScaleOffset.y;

					//シェーダー側で必要な、Cookieをサンプリングするための座標変換の行列を計算する。
					//URPのコードを移植。
					Matrix4x4 worldToLight = light.localToWorldMatrix.inverse;
					if (light.lightType == LightType.Spot)
					{
						Matrix4x4 perspective = Matrix4x4.Perspective(light.spotAngle, 1, 0.001f, light.range);

						// Cancel embedded camera view axis flip (https://docs.unity3d.com/2021.1/Documentation/ScriptReference/Matrix4x4.Perspective.html)
						perspective.SetColumn(2, perspective.GetColumn(2) * -1);

						// world -> light local -> light perspective
						worldToLight = perspective * worldToLight;
					}
					else if (light.lightType == LightType.Directional)
					{
						Matrix4x4 cookieUvTransform = Matrix4x4.identity;
						Vector2 uvScale = Vector2.one / light.light.cookieSize2D;
						Vector2 uvOffset = Vector2.zero;//TODO: offsetの実装。

						if (Mathf.Abs(uvScale.x) < half.MinValue) uvScale.x = Mathf.Sign(uvScale.x) * half.MinValue;
						if (Mathf.Abs(uvScale.y) < half.MinValue) uvScale.y = Mathf.Sign(uvScale.y) * half.MinValue;

						cookieUvTransform = Matrix4x4.Scale(new Vector3(uvScale.x, uvScale.y, 1));
						cookieUvTransform.SetColumn(3, new Vector4(-uvOffset.x * uvScale.x, -uvOffset.y * uvScale.y, 0, 1));

						worldToLight = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, -0.5f, 0.5f) * cookieUvTransform * worldToLight;
					}
					passData.Data[i] = new()
					{
						WorldToLight = worldToLight,
						UvScaleOffset = uvScaleOffset,
						WrapMode = (float)cookie.wrapMode,
					};
				}

				cmd.SetGlobalTexture(IdLightCookieAtlas, cookieAtlas.AtlasTexture);

				passData.ComputeBuffer.SetData(passData.Data, 0, 0, passData.CookieCount);
				cmd.SetGlobalBuffer(IdLightCookieBuffer, passData.ComputeBuffer);
			});
		}

		private static int ComputeOctahedralCookieSize(Texture cookie)
		{
			// Map 6*WxH pixels into 2W*2H pixels, so 4/6 ratio or 66% of cube pixels.
			int octCookieSize = Math.Max(cookie.width, cookie.height);
			octCookieSize = (int)(octCookieSize * 2.5f + 0.5f);
			return octCookieSize;
		}
	}
}