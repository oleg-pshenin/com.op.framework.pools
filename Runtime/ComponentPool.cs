using System;
using System.Collections.Generic;
using UnityEngine;

namespace OP.Framework.Pools
{
    /// <summary>
    /// Unity specific component pool wrapper that manages GameObject activation, Transform set and reset, hierarchy placement, and spawn/release interface calls.
    /// Expects 1 component <-> 1 gameobject relationships, not independent components, many to one, many to many etc.
    /// </summary>
    public class ComponentPool<TComponent> where TComponent : Component
    {
        private readonly ObjectPool<TComponent> _pool;
        private readonly Transform _poolRoot;

        public IReadOnlyList<TComponent> Instances => _pool.Instances;

        public ComponentPool(Func<TComponent> factoryMethod, Transform poolRoot)
        {
            _poolRoot = poolRoot;
            _pool = new ObjectPool<TComponent>(() =>
            {
                var instance = factoryMethod.Invoke();
                if (instance == null)
                    return null;

                instance.gameObject.SetActive(false);
                if (instance.transform.parent != _poolRoot)
                    instance.transform.SetParent(_poolRoot, false);
                return instance;
            }, instance => instance != null);
        }

        public TComponent Get(Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return GetInternal(instanceRoot, setAsLastSibling, false, default, default);
        }

        public TComponent Get(Vector3 position, Quaternion rotation, Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return GetInternal(instanceRoot, setAsLastSibling, true, position, rotation);
        }

        private TComponent GetInternal(Transform instanceRoot, bool setAsLastSibling, bool setPose, Vector3 position, Quaternion rotation)
        {
            var instance = _pool.Get();
            if (instance == null)
                return null;

            var targetRoot = instanceRoot != null ? instanceRoot : _poolRoot;

            var transform = instance.transform;
            if (transform.parent != targetRoot)
                transform.SetParent(targetRoot, false);

            // the idea is that if you take from pool without specifying pose, it can be random, but good to have reset state of transform as if you would instantiate
            // otherwise, normalizing scale, to avoid reparenting artifacts
            if (setPose)
            {
                transform.SetPositionAndRotation(position, rotation);
                transform.localScale = Vector3.one;
            }
            else
            {
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
            }

            if (setAsLastSibling)
                transform.SetAsLastSibling();

            instance.gameObject.SetActive(true);
            if (instance is ISpawnable spawnable)
                spawnable.Spawn();
            return instance;
        }

        public bool Release(TComponent instance)
        {
            if (!_pool.Release(instance))
                return false;

            instance.gameObject.SetActive(false);

            // should be called after set active false or before? - probably yes to avoid 
            if (instance is IReleasable releasable)
                releasable.Release();

            if (instance.transform.parent != _poolRoot)
                instance.transform.SetParent(_poolRoot, false);

            return true;
        }

        public void Remove(TComponent instance)
        {
            _pool.Remove(instance);
        }

        public void ReleaseAll()
        {
            for (var i = _pool.Instances.Count - 1; i >= 0; i--)
                Release(_pool.Instances[i]);
        }

        public void Prewarm(int count)
        {
            _pool.Prewarm(count);
        }
    }
}