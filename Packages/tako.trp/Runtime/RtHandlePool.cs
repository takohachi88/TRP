using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp
{
	public class RtHandlePool
	{
		public static RtHandlePool Instance
		{
			get
			{
				if (_instance == null) _instance = new(16);
				return _instance;
			}
		}
		private static RtHandlePool _instance;
		private Dictionary<(int, int, RTHandleAllocInfo), RTHandle> _pool;

		public void Initialize(int capacity = 16)
		{
			_instance = new(capacity);
		}

		public RtHandlePool(int capacity)
		{
			_pool = new(capacity);
		}

		public RTHandle GetOrAlloc(Vector2Int size, RTHandleAllocInfo allocInfo)
		{
			var key = (size.x, size.y, allocInfo);
			if (_pool.ContainsKey(key)) return _pool[key];
			else
			{
				RTHandle handle = RTHandles.Alloc(size.x, size.y, allocInfo);
				_pool.Add(key, handle);
				return handle;
			}
		}

		public void Dispose()
		{
#if UNITY_EDITOR
			string log = string.Empty;
#endif
			foreach (RTHandle handle in _pool.Values)
			{
#if UNITY_EDITOR
				log += $"{handle.name}, {handle.referenceSize}\n";
#endif
				handle.Release();
			}

#if UNITY_EDITOR
			Debug.Log($"{_pool.Count} RTHandles were released. \n{log}");
#endif
			_pool.Clear();
			_pool = null;
			_instance = null;
			GC.Collect();
		}
	}
}