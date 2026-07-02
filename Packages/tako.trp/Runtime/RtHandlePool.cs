using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Trp
{
	/// <summary>
	/// カメラの中間描画先として使用するRTHandleを、生成条件ごとに再利用するプール。
	/// 解像度変更のたびにRTHandleを破棄・再生成することを避け、確保コストとメモリの揺れを抑える。
	/// プールが所有するRTHandleは呼び出し側でReleaseせず、パイプライン終了時に本クラスがまとめて解放する。
	/// </summary>
	public sealed class RtHandlePool : IDisposable
	{
		/// <summary>通常時に保持するRTHandle数の目安。実使用中のRTHandleを守るため、厳密な上限ではない。</summary>
		private const int DefaultCapacity = 16;
		/// <summary>現在フレームに加え、ここで指定した過去フレーム数まで解放対象から除外する。</summary>
		private const int ProtectedFrameCount = 1;

		/// <summary>レンダーパイプライン内で共有するプール。必要になるまでインスタンスを生成しない。</summary>
		public static RtHandlePool Instance => _instance ??= new RtHandlePool(DefaultCapacity);
		private static RtHandlePool _instance;

		// RTHandleAllocInfoはそのままDictionaryのキーにせず、再利用判定に必要な値だけをPoolKeyへ固定する。
		private readonly Dictionary<PoolKey, PoolEntry> _pool;
		// カメラスタック内で使用中のRTHandleを破棄しないため、容量はsoft capacityとして扱う。
		private int _capacity;

		/// <summary>RTHandle本体と、LRU判定に使用する最終利用フレームを保持する。</summary>
		private sealed class PoolEntry
		{
			public readonly RTHandle Handle;
			public int LastUsedFrame;

			public PoolEntry(RTHandle handle, int lastUsedFrame)
			{
				Handle = handle;
				LastUsedFrame = lastUsedFrame;
			}
		}

		/// <summary>
		/// 同じRTHandleを再利用できるか判定するためのキー。
		/// サイズだけでなく、フォーマット、サンプリング設定、MSAA、用途名も一致する場合だけ再利用する。
		/// </summary>
		private readonly struct PoolKey : IEquatable<PoolKey>
		{
			private readonly int _width;
			private readonly int _height;
			private readonly GraphicsFormat _format;
			private readonly FilterMode _filterMode;
			private readonly TextureWrapMode _wrapModeU;
			private readonly TextureWrapMode _wrapModeV;
			private readonly MSAASamples _msaaSamples;
			private readonly bool _bindTextureMs;
			private readonly string _name;

			public PoolKey(int width, int height, in RTHandleAllocInfo info)
			{
				_width = width;
				_height = height;
				_format = info.format;
				_filterMode = info.filterMode;
				_wrapModeU = info.wrapModeU;
				_wrapModeV = info.wrapModeV;
				_msaaSamples = info.msaaSamples;
				_bindTextureMs = info.bindTextureMS;
				_name = info.name;
			}

			public bool Equals(PoolKey other)
			{
				return _width == other._width &&
				       _height == other._height &&
				       _format == other._format &&
				       _filterMode == other._filterMode &&
				       _wrapModeU == other._wrapModeU &&
				       _wrapModeV == other._wrapModeV &&
				       _msaaSamples == other._msaaSamples &&
				       _bindTextureMs == other._bindTextureMs &&
				       string.Equals(_name, other._name, StringComparison.Ordinal);
			}

			public override bool Equals(object obj) => obj is PoolKey other && Equals(other);

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = _width;
					hash = hash * 397 ^ _height;
					hash = hash * 397 ^ (int)_format;
					hash = hash * 397 ^ (int)_filterMode;
					hash = hash * 397 ^ (int)_wrapModeU;
					hash = hash * 397 ^ (int)_wrapModeV;
					hash = hash * 397 ^ (int)_msaaSamples;
					hash = hash * 397 ^ (_bindTextureMs ? 1 : 0);
					hash = hash * 397 ^ (_name == null ? 0 : StringComparer.Ordinal.GetHashCode(_name));
					return hash;
				}
			}
		}

		public RtHandlePool(int capacity)
		{
			// 1以下ではカメラ間の一時的な切り替えにも耐えられないため、最低2枠を確保する。
			_capacity = Mathf.Max(2, capacity);
			_pool = new Dictionary<PoolKey, PoolEntry>(_capacity);
		}

		/// <summary>
		/// 保持数の目安を設定する。既存のプールは作り直さず、不要な古いRTHandleだけを整理する。
		/// </summary>
		public void Initialize(int capacity = DefaultCapacity)
		{
			_capacity = Mathf.Max(2, capacity);
			TrimIfNeeded(Time.frameCount);
		}

		/// <summary>
		/// 指定条件と一致するRTHandleを返す。存在しない場合のみ新規確保する。
		/// 取得したRTHandleの所有権はプールに残るため、呼び出し側でReleaseしてはならない。
		/// </summary>
		public RTHandle GetOrAlloc(Vector2Int size, RTHandleAllocInfo allocInfo)
		{
			PoolKey key = new(size.x, size.y, in allocInfo);
			int frame = Time.frameCount;

			if (_pool.TryGetValue(key, out PoolEntry entry))
			{
				// 再利用した時点を記録し、最近使われたRTHandleが優先して残るようにする。
				entry.LastUsedFrame = frame;
				return entry.Handle;
			}

			RTHandle handle = RTHandles.Alloc(size.x, size.y, allocInfo);
			_pool.Add(key, new PoolEntry(handle, frame));
			TrimIfNeeded(frame);
			return handle;
		}

		/// <summary>
		/// soft capacityを超えた場合、保護期間外で最も長く使われていないRTHandleから解放する。
		/// 現在または直前フレームで使われたものしかない場合は、安全を優先して一時的な超過を許可する。
		/// </summary>
		private void TrimIfNeeded(int frame)
		{
			while (_pool.Count > _capacity)
			{
				PoolKey oldestKey = default;
				PoolEntry oldestEntry = null;

				foreach (KeyValuePair<PoolKey, PoolEntry> pair in _pool)
				{
					if (pair.Value.LastUsedFrame >= frame - ProtectedFrameCount) continue;
					if (oldestEntry != null && oldestEntry.LastUsedFrame <= pair.Value.LastUsedFrame) continue;
					oldestKey = pair.Key;
					oldestEntry = pair.Value;
				}

				// すべてが保護期間内なら、カメラスタックが参照中のattachmentである可能性がある。
				// この場合は容量超過のまま残し、次回以降の取得時に改めて整理する。
				if (oldestEntry == null) break;

				oldestEntry.Handle.Release();
				_pool.Remove(oldestKey);
			}
		}

		/// <summary>
		/// プールが所有する全RTHandleを解放する。
		/// Release後のネイティブリソース回収はUnityへ任せ、フレーム停止を招くGC.Collectは呼ばない。
		/// </summary>
		public void Dispose()
		{
			foreach (PoolEntry entry in _pool.Values) entry.Handle.Release();
			_pool.Clear();
			if (ReferenceEquals(_instance, this)) _instance = null;
		}
	}
}
