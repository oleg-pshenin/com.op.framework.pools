using System;
using System.Collections.Generic;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public sealed class PoolHandle<TComponent> : IDisposable where TComponent : Component
    {
        private readonly IPoolHandleOwner _owner;
        private readonly IPoolEntry<TComponent> _entry;

        public bool IsDisposed => _entry.IsDisposed;
        public IReadOnlyList<TComponent> Instances => _entry.Instances;

        internal PoolHandle(IPoolHandleOwner owner, IPoolEntry<TComponent> entry)
        {
            _owner = owner;
            _entry = entry;
        }

        public TComponent Get(Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return _entry.Get(instanceRoot, setAsLastSibling);
        }

        public TComponent Get(Vector3 position, Quaternion rotation, Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return _entry.Get(position, rotation, instanceRoot, setAsLastSibling);
        }

        public void ReleaseAll()
        {
            _entry.ReleaseAll();
        }

        public void Prewarm(int count)
        {
            _entry.Prewarm(count);
        }

        public void Dispose()
        {
            _owner.Dispose(_entry);
        }
    }
}