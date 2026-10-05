#if UNITY_EDITOR
namespace OP.Framework.Pools.SharedPool
{
    internal enum PoolInstanceDebugState
    {
        Pooled,
        Active,
        LostActive,
        LostPooled
    }
}
#endif