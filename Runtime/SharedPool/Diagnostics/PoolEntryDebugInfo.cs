#if UNITY_EDITOR
using System.Collections.Generic;

namespace OP.Framework.Pools.SharedPool
{
    internal sealed class PoolEntryDebugInfo
    {
        public int Id { get; }
        public string Name { get; }
        public PoolEntryDebugKind Kind { get; }
        public string PrefabName { get; }
        public string ComponentTypeName { get; }
        public string CallerMember { get; }
        public string CallerFile { get; }
        public int CallerLine { get; }
        public int CreatedCount { get; }
        public int PeakActive { get; }
        public IReadOnlyList<PoolInstanceDebugInfo> Instances { get; }

        public PoolEntryDebugInfo(
            int id,
            string name,
            PoolEntryDebugKind kind,
            string prefabName,
            string componentTypeName,
            string callerMember,
            string callerFile,
            int callerLine,
            int createdCount,
            int peakActive,
            IReadOnlyList<PoolInstanceDebugInfo> instances)
        {
            Id = id;
            Name = name;
            Kind = kind;
            PrefabName = prefabName;
            ComponentTypeName = componentTypeName;
            CallerMember = callerMember;
            CallerFile = callerFile;
            CallerLine = callerLine;
            CreatedCount = createdCount;
            PeakActive = peakActive;
            Instances = instances;
        }
    }
}
#endif