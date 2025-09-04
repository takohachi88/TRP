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

		public RTHandle Get(Vector2Int size, RTHandleAllocInfo allocInfo)
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
			foreach (RTHandle handle in _pool.Values) handle.Release();
			_pool.Clear();
			_instance = null;
		}
	}
}