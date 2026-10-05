using System.Collections.Generic;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    internal interface IPoolEntry
    {
        bool IsDisposed { get; }
        Component Get(Transform instanceRoot, bool setAsLastSibling);
        Component Get(Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling);
        bool Release(Component instance);
        void Remove(Component instance);
        void ReleaseAll();
        void Prewarm(int count);
        void Dispose();
    }

    internal interface IPoolEntry<out TComponent> : IPoolEntry where TComponent : Component
    {
        IReadOnlyList<TComponent> Instances { get; }
        new TComponent Get(Transform instanceRoot, bool setAsLastSibling);
        new TComponent Get(Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling);
    }
}