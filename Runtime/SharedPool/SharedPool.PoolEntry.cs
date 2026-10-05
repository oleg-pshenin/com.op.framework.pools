using System;
using System.Collections.Generic;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public partial class SharedPool
    {
        private sealed class PoolEntry<TComponent> : IPoolEntry<TComponent> where TComponent : Component
        {
            private readonly SharedPool _owner;
            private readonly ComponentPool<TComponent> _pool;
            private readonly Transform _poolRoot;
            private bool _disposed;

            bool IPoolEntry.IsDisposed => _disposed;
            IReadOnlyList<TComponent> IPoolEntry<TComponent>.Instances => _pool.Instances;

            public PoolEntry(SharedPool owner, TComponent prefab, Transform poolRoot, Action<TComponent> initialize)
            {
                _owner = owner;
                _poolRoot = poolRoot;
                _pool = new ComponentPool<TComponent>(() =>
                {
                    var instance = owner.InstantiateInternal(prefab, poolRoot);
                    initialize?.Invoke(instance);
                    owner.DebugRegisterCreatedInstance(this, instance);
                    return instance;
                }, poolRoot);
            }

            Component IPoolEntry.Get(Transform instanceRoot, bool setAsLastSibling)
            {
                return (this as IPoolEntry<TComponent>).Get(instanceRoot, setAsLastSibling);
            }

            TComponent IPoolEntry<TComponent>.Get(Transform instanceRoot, bool setAsLastSibling)
            {
                if (!ValidateNotDisposed())
                    return null;

                var instance = _pool.Get(instanceRoot, setAsLastSibling);
                _owner.RegisterActiveInstance(instance, this);
                return instance;
            }

            Component IPoolEntry.Get(Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling)
            {
                return (this as IPoolEntry<TComponent>).Get(position, rotation, instanceRoot, setAsLastSibling);
            }

            TComponent IPoolEntry<TComponent>.Get(Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling)
            {
                if (!ValidateNotDisposed())
                    return null;

                var instance = _pool.Get(position, rotation, instanceRoot, setAsLastSibling);
                _owner.RegisterActiveInstance(instance, this);
                return instance;
            }

            bool IPoolEntry.Release(Component instance)
            {
                if (!ValidateNotDisposed())
                    return false;

                return _pool.Release((TComponent)instance);
            }

            void IPoolEntry.Remove(Component instance)
            {
                if (!ValidateNotDisposed())
                    return;

                _pool.Remove((TComponent)instance);
            }

            void IPoolEntry.ReleaseAll()
            {
                if (ValidateNotDisposed())
                    ReleaseAllInternal();
            }

            void IPoolEntry.Prewarm(int count)
            {
                if (ValidateNotDisposed())
                    _pool.Prewarm(count);
            }

            void IPoolEntry.Dispose()
            {
                ReleaseAllInternal();
                _disposed = true;
                if (_poolRoot != null)
                    Destroy(_poolRoot.gameObject);
            }

            private void ReleaseAllInternal()
            {
                for (var i = _pool.Instances.Count - 1; i >= 0; i--)
                    _owner.ReleaseInternal(_pool.Instances[i]);
            }

            private bool ValidateNotDisposed()
            {
                if (!_disposed)
                    return true;

                Log.LogError($"Trying to use disposed pool for {typeof(TComponent).Name}.");
                return false;
            }
        }
    }
}