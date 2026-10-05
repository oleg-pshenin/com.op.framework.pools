using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public partial class SharedPool
    {
#if UNITY_EDITOR
        private sealed class PoolEntryDebugRecord
        {
            public int Id;
            public string Name;
            public PoolEntryDebugKind Kind;
            public string PrefabName;
            public string ComponentTypeName;
            public string CallerMember;
            public string CallerFile;
            public int CallerLine;
            public int ActiveCount;
            public int PeakActive;
            public readonly List<PoolInstanceDebugRecord> Instances = new();
        }

        private sealed class PoolScopeDebugRecord
        {
            public int Id;
            public string Name;
            public string CallerMember;
            public string CallerFile;
            public int CallerLine;
        }

        private sealed class PoolInstanceDebugRecord
        {
            public int InstanceId;
            public string Name;
            public Component Instance;
            public PoolEntryDebugRecord Entry;
            public PoolScope Scope;
            public PoolInstanceDebugState State;
        }

        private readonly Dictionary<IPoolEntry, PoolEntryDebugRecord> _debugEntries = new();
        private readonly Dictionary<PoolScope, PoolScopeDebugRecord> _debugScopes = new();
        private readonly Dictionary<int, PoolInstanceDebugRecord> _debugInstancesByUnityId = new();
        private int _debugEntryIndex;
        private int _debugScopeIndex;

        internal static bool TryGetDebugSnapshot(out PoolDebugSnapshot snapshot)
        {
            if (Instance == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = Instance.CreateDebugSnapshot();
            return true;
        }

        private PoolDebugSnapshot CreateDebugSnapshot()
        {
            ScanLostDebugInstances();
            var entries = new List<PoolEntryDebugInfo>(_debugEntries.Count);
            var entryRecords = new List<PoolEntryDebugRecord>(_debugEntries.Values);
            entryRecords.Sort((a, b) => a.Id.CompareTo(b.Id));

            foreach (var entry in entryRecords)
            {
                var instances = new List<PoolInstanceDebugInfo>(entry.Instances.Count);
                foreach (var instance in entry.Instances)
                    instances.Add(CreateDebugInfo(instance));
                entries.Add(new PoolEntryDebugInfo(entry.Id, entry.Name, entry.Kind, entry.PrefabName, entry.ComponentTypeName, entry.CallerMember, entry.CallerFile, entry.CallerLine,
                    entry.Instances.Count, entry.PeakActive, instances));
            }

            var scopes = new List<PoolScopeDebugInfo>(_debugScopes.Count);
            var scopePairs = new List<KeyValuePair<PoolScope, PoolScopeDebugRecord>>(_debugScopes);
            scopePairs.Sort((a, b) => a.Value.Id.CompareTo(b.Value.Id));

            foreach (var pair in scopePairs)
            {
                var instances = new List<PoolInstanceDebugInfo>();
                foreach (var entry in entryRecords)
                {
                    foreach (var instance in entry.Instances)
                    {
                        if (ReferenceEquals(instance.Scope, pair.Key))
                            instances.Add(CreateDebugInfo(instance));
                    }
                }

                scopes.Add(new PoolScopeDebugInfo(pair.Value.Id, pair.Value.Name, pair.Value.CallerMember, pair.Value.CallerFile, pair.Value.CallerLine, instances));
            }

            return new PoolDebugSnapshot(entries, scopes);
        }

        private static PoolInstanceDebugInfo CreateDebugInfo(PoolInstanceDebugRecord instance)
        {
            return new PoolInstanceDebugInfo(instance.InstanceId, instance.Name, instance.Instance, instance.State);
        }

        private void ScanLostDebugInstances()
        {
            foreach (var entry in _debugEntries.Values)
            foreach (var instance in entry.Instances)
            {
                if (instance.State != PoolInstanceDebugState.Active && instance.State != PoolInstanceDebugState.Pooled)
                    continue;
                if (instance.Instance != null)
                {
                    instance.Name = instance.Instance.name;
                    continue;
                }

                if (instance.State == PoolInstanceDebugState.Active)
                {
                    instance.State = PoolInstanceDebugState.LostActive;
                    if (entry.ActiveCount > 0)
                        entry.ActiveCount--;
                }
                else
                {
                    instance.State = PoolInstanceDebugState.LostPooled;
                }
            }
        }
#endif

        [Conditional("UNITY_EDITOR")]
        private void DebugRegisterEntry(IPoolEntry entry, Component prefab, bool isHandle, string debugName = null, string callerMember = null, string callerFile = null, int callerLine = 0)
        {
#if UNITY_EDITOR
            if (entry == null || prefab == null || _debugEntries.ContainsKey(entry))
                return;
            var id = ++_debugEntryIndex;
            var name = isHandle ? string.IsNullOrWhiteSpace(debugName) ? $"{prefab.name} #{id}" : debugName : prefab.name;
            _debugEntries.Add(entry, new PoolEntryDebugRecord
            {
                Id = id, Name = name, Kind = isHandle ? PoolEntryDebugKind.Handle : PoolEntryDebugKind.Shared, PrefabName = prefab.name, ComponentTypeName = prefab.GetType().Name,
                CallerMember = callerMember, CallerFile = callerFile, CallerLine = callerLine
            });
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugDisposeEntry(IPoolEntry entry)
        {
#if UNITY_EDITOR
            if (entry == null || !_debugEntries.TryGetValue(entry, out var debugEntry))
                return;
            _debugEntries.Remove(entry);
            foreach (var instance in debugEntry.Instances)
                if (_debugInstancesByUnityId.TryGetValue(instance.InstanceId, out var current) && ReferenceEquals(current, instance))
                    _debugInstancesByUnityId.Remove(instance.InstanceId);
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugRegisterCreatedInstance(IPoolEntry entry, Component instance)
        {
#if UNITY_EDITOR
            if (entry == null || instance == null || !_debugEntries.TryGetValue(entry, out var debugEntry))
                return;
            var instanceId = instance.GetInstanceID();
            if (_debugInstancesByUnityId.TryGetValue(instanceId, out var existing) && ReferenceEquals(existing.Instance, instance))
                return;
            var debugInstance = new PoolInstanceDebugRecord { InstanceId = instanceId, Name = instance.name, Instance = instance, Entry = debugEntry, State = PoolInstanceDebugState.Pooled };
            debugEntry.Instances.Add(debugInstance);
            _debugInstancesByUnityId[instanceId] = debugInstance;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugMarkActiveInstance(Component instance, IPoolEntry entry)
        {
#if UNITY_EDITOR
            if (instance == null || !_debugInstancesByUnityId.TryGetValue(instance.GetInstanceID(), out var debugInstance))
                return;
            if (!_debugEntries.TryGetValue(entry, out var debugEntry) || !ReferenceEquals(debugInstance.Entry, debugEntry))
                return;
            if (debugInstance.State != PoolInstanceDebugState.Active)
                debugEntry.ActiveCount++;
            debugInstance.State = PoolInstanceDebugState.Active;
            debugInstance.Scope = null;
            if (debugEntry.ActiveCount > debugEntry.PeakActive)
                debugEntry.PeakActive = debugEntry.ActiveCount;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugMarkPooledInstance(Component instance)
        {
#if UNITY_EDITOR
            if (instance == null || !_debugInstancesByUnityId.TryGetValue(instance.GetInstanceID(), out var debugInstance))
                return;
            if (debugInstance.State == PoolInstanceDebugState.Active && debugInstance.Entry.ActiveCount > 0)
                debugInstance.Entry.ActiveCount--;
            debugInstance.State = PoolInstanceDebugState.Pooled;
            debugInstance.Scope = null;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugMarkLostActiveInstance(IPoolEntry entry, Component instance)
        {
#if UNITY_EDITOR
            if (entry == null || ReferenceEquals(instance, null) || !_debugEntries.TryGetValue(entry, out var debugEntry))
                return;
            foreach (var debugInstance in debugEntry.Instances)
            {
                if (!ReferenceEquals(debugInstance.Instance, instance))
                    continue;
                if (debugInstance.State == PoolInstanceDebugState.Active && debugEntry.ActiveCount > 0)
                    debugEntry.ActiveCount--;
                debugInstance.State = PoolInstanceDebugState.LostActive;
                return;
            }
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugRegisterScope(PoolScope scope, string debugName = null, string callerMember = null, string callerFile = null, int callerLine = 0)
        {
#if UNITY_EDITOR
            if (scope == null || _debugScopes.ContainsKey(scope))
                return;
            var id = ++_debugScopeIndex;
            _debugScopes.Add(scope,
                new PoolScopeDebugRecord
                    { Id = id, Name = string.IsNullOrWhiteSpace(debugName) ? $"Scope #{id}" : debugName, CallerMember = callerMember, CallerFile = callerFile, CallerLine = callerLine });
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugAssignScope(PoolScope scope, Component instance)
        {
#if UNITY_EDITOR
            if (scope == null || instance == null || !_debugInstancesByUnityId.TryGetValue(instance.GetInstanceID(), out var debugInstance))
                return;
            debugInstance.Scope = scope;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugDisposeScope(PoolScope scope)
        {
#if UNITY_EDITOR
            if (scope == null)
                return;
            _debugScopes.Remove(scope);
            foreach (var entry in _debugEntries.Values)
            foreach (var instance in entry.Instances)
                if (ReferenceEquals(instance.Scope, scope))
                    instance.Scope = null;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugReset()
        {
#if UNITY_EDITOR
            _debugEntries.Clear();
            _debugScopes.Clear();
            _debugInstancesByUnityId.Clear();
            _debugEntryIndex = 0;
            _debugScopeIndex = 0;
#endif
        }
    }
}