#if UNITY_EDITOR
using System.Collections.Generic;

namespace OP.Framework.Pools.SharedPool
{
    internal sealed class PoolScopeDebugInfo
    {
        public int Id { get; }
        public string Name { get; }
        public string CallerMember { get; }
        public string CallerFile { get; }
        public int CallerLine { get; }
        public IReadOnlyList<PoolInstanceDebugInfo> Instances { get; }

        public PoolScopeDebugInfo(int id, string name, string callerMember, string callerFile, int callerLine, IReadOnlyList<PoolInstanceDebugInfo> instances)
        {
            Id = id;
            Name = name;
            CallerMember = callerMember;
            CallerFile = callerFile;
            CallerLine = callerLine;
            Instances = instances;
        }
    }
}
#endif