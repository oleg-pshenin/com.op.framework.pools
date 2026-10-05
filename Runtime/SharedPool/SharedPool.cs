using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public partial class SharedPool : MonoBehaviour, IPoolHandleOwner, IPoolScopeOwner
    {
        private readonly Dictionary<Component, IPoolEntry> _sharedEntriesByPrefab = new();
        private readonly HashSet<IPoolEntry> _scopedEntries = new();
        private readonly Dictionary<Component, IPoolEntry> _entryByActiveInstance = new();
        private readonly Dictionary<PoolScope, HashSet<Component>> _instancesByScope = new();
        private readonly Dictionary<Component, PoolScope> _scopeByActiveInstance = new();

        private int _scopedPoolIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Log.LogError("Only one SharedPool instance can exist at a time.", this);
                return;
            }

            Instance = this;
            transform.SetParent(null, false);
        }

        private void OnDestroy()
        {
            _sharedEntriesByPrefab.Clear();
            _scopedEntries.Clear();
            _entryByActiveInstance.Clear();

            foreach (var scope in _instancesByScope.Keys)
                scope.MarkDisposed();

            _instancesByScope.Clear();
            _scopeByActiveInstance.Clear();
            DebugReset();
            if (Instance == this)
                Instance = null;
        }

        protected virtual TComponent InstantiateInternal<TComponent>(TComponent prefab, Transform instanceRoot) where TComponent : Component
        {
            return Instantiate(prefab, instanceRoot);
        }

        private TComponent GetShared<TComponent>(TComponent prefab, Transform instanceRoot, bool setAsLastSibling) where TComponent : Component
        {
            return (TComponent)GetOrCreateSharedEntry(prefab)?.Get(instanceRoot, setAsLastSibling);
        }

        private TComponent GetShared<TComponent>(TComponent prefab, Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling) where TComponent : Component
        {
            return (TComponent)GetOrCreateSharedEntry(prefab)?.Get(position, rotation, instanceRoot, setAsLastSibling);
        }

        private PoolHandle<TComponent> CreateHandleInternal<TComponent>(
            TComponent prefab,
            Action<TComponent> initialize,
            string debugName = null,
            string callerMember = null,
            string callerFile = null,
            int callerLine = 0) where TComponent : Component
        {
            if (!ValidatePrefab(prefab))
                return null;

            var poolRoot = CreatePoolRoot($"Scoped {_scopedPoolIndex++}", prefab);
            var entry = new PoolEntry<TComponent>(this, prefab, poolRoot, initialize);
            _scopedEntries.Add(entry);
            DebugRegisterEntry(entry, prefab, true, debugName, callerMember, callerFile, callerLine);

            return new PoolHandle<TComponent>(this, entry);
        }

        private PoolScope CreateScopeInternal(string debugName = null, string callerMember = null, string callerFile = null, int callerLine = 0)
        {
            var scope = new PoolScope(this);
            _instancesByScope.Add(scope, new HashSet<Component>());
            DebugRegisterScope(scope, debugName, callerMember, callerFile, callerLine);

            return scope;
        }

        private IPoolEntry GetOrCreateSharedEntry<TComponent>(TComponent prefab) where TComponent : Component
        {
            if (!ValidatePrefab(prefab))
                return null;

            if (_sharedEntriesByPrefab.TryGetValue(prefab, out var entry))
                return entry;

            entry = new PoolEntry<TComponent>(this, prefab, CreatePoolRoot("Shared", prefab), null);
            _sharedEntriesByPrefab.Add(prefab, entry);
            DebugRegisterEntry(entry, prefab, false);

            return entry;
        }

        private Transform CreatePoolRoot(string prefix, Component prefab)
        {
            var root = new GameObject($"{prefix} - {prefab.name} [{prefab.GetType().Name}]").transform;
            root.SetParent(transform, false);

            return root;
        }

        private bool ValidatePrefab(Component prefab)
        {
            if (prefab != null)
                return true;

            Log.LogError("SharedPool prefab is null or destroyed.", prefab);
            return false;
        }

        private void RegisterActiveInstance(Component instance, IPoolEntry entry)
        {
            if (_entryByActiveInstance.ContainsKey(instance))
            {
                Log.LogError($"Instance '{instance.name}' is already active in SharedPool.", instance);
                return;
            }

            if (!RegisterGameObjectMapping(instance))
                return;

            _entryByActiveInstance.Add(instance, entry);
            DebugMarkActiveInstance(instance, entry);
        }

        private void RegisterScopeInstance(PoolScope scope, Component instance)
        {
            var instances = _instancesByScope[scope];
            if (instances.Contains(instance))
            {
                Log.LogError($"Instance '{instance.name}' is already tracked by this PoolScope.", instance);
                return;
            }

            if (_scopeByActiveInstance.ContainsKey(instance))
            {
                Log.LogError($"Instance '{instance.name}' is already owned by another PoolScope.", instance);
                return;
            }

            instances.Add(instance);
            _scopeByActiveInstance.Add(instance, scope);
            DebugAssignScope(scope, instance);
        }

        private void ReleaseInternal(Component instance, PoolScope expectedScope = null)
        {
            if (ReferenceEquals(instance, null))
            {
                Log.LogError("Trying to release a null instance in SharedPool.");
                return;
            }

            if (!_entryByActiveInstance.TryGetValue(instance, out var entry))
            {
                Log.LogError("Trying to release an instance that is not active in SharedPool.", instance);
                return;
            }

            if (expectedScope != null && (!_scopeByActiveInstance.TryGetValue(instance, out var ownerScope) || ownerScope != expectedScope))
            {
                Log.LogError("Instance does not belong to this PoolScope.", instance);
                return;
            }

            if (instance == null)
            {
                Log.LogError("Active SharedPool instance was destroyed without being released.");
                UnregisterActiveInstance(instance);
                entry.Remove(instance);
                DebugMarkLostActiveInstance(entry, instance);
                return;
            }

            if (!entry.Release(instance))
            {
                Log.LogError("SharedPool tracks the instance as active, but its backing pool does not.", instance);
                return;
            }

            UnregisterActiveInstance(instance);
            DebugMarkPooledInstance(instance);
        }

        private void UnregisterActiveInstance(Component instance)
        {
            _entryByActiveInstance.Remove(instance);
            UnregisterGameObjectMapping(instance);

            if (!_scopeByActiveInstance.Remove(instance, out var scope))
                return;

            if (_instancesByScope.TryGetValue(scope, out var instances))
                instances.Remove(instance);
        }

        void IPoolHandleOwner.Dispose(IPoolEntry entry)
        {
            if (entry.IsDisposed)
                return;

            if (!_scopedEntries.Remove(entry))
            {
                Log.LogError("PoolHandle does not reference an active scoped pool.");
                return;
            }

            entry.Dispose();
            DebugDisposeEntry(entry);
        }

        TComponent IPoolScopeOwner.Get<TComponent>(PoolScope scope, TComponent prefab, Transform instanceRoot, bool setAsLastSibling)
        {
            if (!ValidateScope(scope))
                return null;

            var instance = GetShared(prefab, instanceRoot, setAsLastSibling);
            RegisterScopeInstance(scope, instance);
            return instance;
        }

        TComponent IPoolScopeOwner.Get<TComponent>(PoolScope scope, TComponent prefab, Vector3 position, Quaternion rotation, Transform instanceRoot, bool setAsLastSibling)
        {
            if (!ValidateScope(scope))
                return null;

            var instance = GetShared(prefab, position, rotation, instanceRoot, setAsLastSibling);
            RegisterScopeInstance(scope, instance);
            return instance;
        }

        void IPoolScopeOwner.Release(PoolScope scope, GameObject instance)
        {
            if (!ValidateScope(scope))
                return;

            ReleaseGameObjectInternal(instance, scope);
        }

        void IPoolScopeOwner.Release(PoolScope scope, Component instance)
        {
            if (!ValidateScope(scope))
                return;

            ReleaseInternal(instance, scope);
        }

        void IPoolScopeOwner.ReleaseAll(PoolScope scope)
        {
            if (!ValidateScope(scope))
                return;

            var instances = _instancesByScope[scope];

            while (instances.Count > 0)
            {
                var instance = instances.First();
                // manually cleaning here, because in release internal handle all kind of desyncs and can skip proper cleanup of instance
                instances.Remove(instance);
                ReleaseInternal(instance);
            }
        }

        void IPoolScopeOwner.Dispose(PoolScope scope)
        {
            // double dispose call is fine, only usage of disposed scope is bad
            if (scope.IsDisposed)
            {
                Log.LogWarning("Trying to dispose a disposed PoolScope.");
                return;
            }

            if (!_instancesByScope.ContainsKey(scope))
            {
                Log.LogError("PoolScope does not belong to this SharedPool.", this);
                return;
            }

            ((IPoolScopeOwner)this).ReleaseAll(scope);
            _instancesByScope.Remove(scope);
            DebugDisposeScope(scope);
            scope.MarkDisposed();
        }

        private bool ValidateScope(PoolScope scope)
        {
            if (scope.IsDisposed)
            {
                Log.LogError("Trying to use a disposed PoolScope.");
                return false;
            }

            if (_instancesByScope.ContainsKey(scope))
                return true;

            Log.LogError("PoolScope does not belong to this SharedPool.");
            return false;
        }
    }
}