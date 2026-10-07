using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public partial class SharedPool
    {
        public static GameObject Get(GameObject prefab, Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return Get(prefab.transform, instanceRoot, setAsLastSibling).gameObject;
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform instanceRoot = null, bool setAsLastSibling = true)
        {
            return Get(prefab.transform, position, rotation, instanceRoot, setAsLastSibling).gameObject;
        }

        public static void Release(GameObject instance)
        {
            Instance.ReleaseGameObjectInternal(instance);
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            Prewarm(prefab.transform, count);
        }

        public static TComponent Get<TComponent>(TComponent prefab, Vector3 position, Quaternion rotation, Transform instanceRoot = null, bool setAsLastSibling = true) where TComponent : Component
        {
            return Instance.GetShared(prefab, position, rotation, instanceRoot, setAsLastSibling);
        }

        public static TComponent Get<TComponent>(TComponent prefab, Transform instanceRoot = null, bool setAsLastSibling = true) where TComponent : Component
        {
            return Instance.GetShared(prefab, instanceRoot, setAsLastSibling);
        }

#if UNITY_EDITOR
        public static PoolHandle<TComponent> CreateHandle<TComponent>(
            TComponent prefab,
            Action<TComponent> initialize,
            string debugName = null,
            [CallerMemberName] string callerMember = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLine = 0) where TComponent : Component
        {
            return Instance.CreateHandleInternal(prefab, initialize, debugName, callerMember, callerFile, callerLine);
        }

        public static PoolScope CreateScope(string debugName = null, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null, [CallerLineNumber] int callerLine = 0)
        {
            return Instance.CreateScopeInternal(debugName, callerMember, callerFile, callerLine);
        }
#else
        public static PoolHandle<TComponent> CreateHandle<TComponent>(TComponent prefab, Action<TComponent> initialize, string debugName = null) where TComponent : Component
        {
            return Instance.CreateHandleInternal(prefab, initialize, debugName);
        }

        public static PoolScope CreateScope(string debugName = null)
        {
            return Instance.CreateScopeInternal(debugName);
        }
#endif

        public static void Release(Component instance)
        {
            // On release we don't care, we will see if the instance is still valid on Get and other calls, but on teardown we don't want to spam false positives.
            if (Instance == null)
                return;
            
            Instance.ReleaseInternal(instance);
        }

        public static void Release(IEnumerable<Component> instances)
        {
            if (instances == null)
                return;

            var releaseBuffer = new List<Component>(instances);
            foreach (var instance in releaseBuffer)
                Instance.ReleaseInternal(instance);
        }

        public static void Prewarm<TComponent>(TComponent prefab, int count) where TComponent : Component
        {
            Instance.GetOrCreateSharedEntry(prefab).Prewarm(count);
        }

        private static SharedPool Instance { get; set; }
    }
}