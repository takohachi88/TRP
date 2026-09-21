using Trp;
using UnityEditor;

namespace TrpEditor
{
	/// <summary>
	/// 追加データの存在はInspectorに表示するが、設定項目はLightのInspectorへ統合する。
	/// </summary>
	[CanEditMultipleObjects]
	[CustomEditor(typeof(TrpLightData))]
	public sealed class TrpLightDataEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			// URPのAdditional Dataと同様に、このコンポーネント自身にはプロパティを描画しない。
		}
	}
}
