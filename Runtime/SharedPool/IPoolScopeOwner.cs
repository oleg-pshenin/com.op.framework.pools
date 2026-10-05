using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    internal interface IPoolScopeOwner
    {
        TComponent Get<TComponent>(PoolScope scope, TComponent prefab, Transform instanceRoot, bool setAsLastSibling) where TComponent : Component;
        TComponent Get<TComponent>(PoolScope scope, TComponent prefab, Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling) where TComponent : Component;
        void Release(PoolScope scope, GameObject instance);
        void Release(PoolScope scope, Component instance);
        void ReleaseAll(PoolScope scope);
        void Dispose(PoolScope scope);
    }
}