namespace OP.Framework.Pools.SharedPool
{
    internal interface IPoolHandleOwner
    {
        void Dispose(IPoolEntry entry);
    }
}