using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/Blur")]
	public class Blur : VolumeComponent, IPostProcessComponent
	{
		// Intensity は 1.0 でおよそ 8 ピクセルのブラー半径。
		public MinFloatParameter intensity = new(0f, 0f);

		// X と Y を個別に拡大し、方向ごとのブラー半径を調整。
		public Vector2Parameter scale = new(new Vector2(1f, 1f));

		// 片側の最大サンプル対数。実際のサンプル数は Intensity に応じてこの値まで増える。
		public NoInterpClampedIntParameter quality = new(5, 1, 16);

		// ControlTexture の R チャンネルで画素ごとのブラー半径を制御。
		public TextureParameter controlTexture = new(null);

		// 有効時は強度に応じてピラミッドを構築し、広いブラーを低負荷で処理する。
		public BoolParameter downSample = new(false);

		public bool IsActive() => 0f < intensity.value;
	}
}
