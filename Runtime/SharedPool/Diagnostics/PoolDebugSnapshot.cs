#if UNITY_EDITOR
using System.Collections.Generic;

namespace OP.Framework.Pools.SharedPool
{
    internal sealed class PoolDebugSnapshot
    {
        public IReadOnlyList<PoolEntryDebugInfo> Entries { get; }
        public IReadOnlyList<PoolScopeDebugInfo> Scopes { get; }

        public PoolDebugSnapshot(IReadOnlyList<PoolEntryDebugInfo> entries, IReadOnlyList<PoolScopeDebugInfo> scopes)
        {
            Entries = entries;
            Scopes = scopes;
        }
    }
}
#endif