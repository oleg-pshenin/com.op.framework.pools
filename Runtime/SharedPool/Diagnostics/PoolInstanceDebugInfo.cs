#if UNITY_EDITOR
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    internal sealed class PoolInstanceDebugInfo
    {
        public int InstanceId { get; }
        public string Name { get; }
        public Component Instance { get; }
        public PoolInstanceDebugState State { get; }

        public PoolInstanceDebugInfo(int instanceId, string name, Component instance, PoolInstanceDebugState state)
        {
            InstanceId = instanceId;
            Name = name;
            Instance = instance;
            State = state;
        }
    }
}
#endif