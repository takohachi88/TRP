using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	[Serializable]
	public sealed class CustomPass
	{
		[SerializeField] private CustomPassObject _passObject;
		public bool Enabled = true;
		public CustomPassObject PassObject => _passObject;
	}

	/// <summary>
	/// TRP外から処理を追加したいときに用いる。
	/// Cameraに登録する。
	/// </summary>
	//[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU + "CustomPassObject", fileName = "CustomPassObject")]
	public abstract class CustomPassObject : ScriptableObject
	{
		[SerializeField] private ExecutionPhase _phase;
		public ExecutionPhase Phase => _phase;
		public abstract void Execute(ref PassParams passParams);
	}
}