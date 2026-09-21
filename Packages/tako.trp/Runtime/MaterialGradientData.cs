using UnityEngine;

namespace Trp
{
	/// <summary>
	/// マテリアルのサブアセットとして保持する Gradient Texture の生成設定。
	/// 実際にシェーダーが参照する Texture2D も同じマテリアル内へ保存する。
	/// </summary>
	public sealed class MaterialGradientData : ScriptableObject
	{
		[SerializeField] private string _propertyName;
		[SerializeField, GradientUsage(true)] private Gradient _gradient;
		[SerializeField, Min(2)] private int _width = 32;
		[SerializeField] private FilterMode _filterMode = FilterMode.Bilinear;
		[SerializeField] private Texture2D _texture;

		public string PropertyName => _propertyName;
		public Gradient Gradient => _gradient;
		public int Width => _width;
		public FilterMode FilterMode => _filterMode;
		public Texture2D Texture => _texture;
	}
}
