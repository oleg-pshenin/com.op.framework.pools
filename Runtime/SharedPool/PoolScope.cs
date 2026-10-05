using System;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public sealed class PoolScope : IDisposable
    {
        private readonly IPoolScopeOwner _owner;

        public bool IsDisposed { get; private set; }

        internal PoolScope(IPoolScopeOwner owner)
        {
            _owner = owner;
        }

        public GameObject Get(GameObject prefab, Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return Get(prefab.transform, instanceRoot, setAsLastSibling).gameObject;
        }

        public void Release(GameObject instance)
        {
            _owner.Release(this, instance);
        }

        public TComponent Get<TComponent>(TComponent prefab, Transform instanceRoot = null, bool setAsLastSibling = true) where TComponent : Component
        {
            return _owner.Get(this, prefab, instanceRoot, setAsLastSibling);
        }

        public TComponent Get<TComponent>(TComponent prefab, Vector3 position, Quaternion rotation, Transform instanceRoot = null, bool setAsLastSibling = true) where TComponent : Component
        {
            return _owner.Get(this, prefab, position, rotation, instanceRoot, setAsLastSibling);
        }

        public void Release(Component instance)
        {
            _owner.Release(this, instance);
        }

        public void ReleaseAll()
        {
            _owner.ReleaseAll(this);
        }

        public void Dispose()
        {
            _owner.Dispose(this);
        }

        internal void MarkDisposed()
        {
            IsDisposed = true;
        }
    }
}