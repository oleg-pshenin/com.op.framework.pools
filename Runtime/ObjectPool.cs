using System;
using System.Collections.Generic;

namespace OP.Framework.Pools
{
    /// <summary>
    /// More or less generic object pool with generic validation (mostly for unity "zombie" layer)
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factoryMethod;
        private readonly Predicate<T> _isValid;
        private readonly List<T> _instances = new();
        private readonly List<T> _pool = new();

        public IReadOnlyList<T> Instances => _instances;

        public ObjectPool(Func<T> factoryMethod, Predicate<T> isValid = null)
        {
            _factoryMethod = factoryMethod;
            _isValid = isValid;
        }

        public void Prewarm(int count)
        {
            if (count < 0)
            {
                Log.LogError($"{GetType().Name} cannot prewarm a negative instance count: {count}.");
                return;
            }

            RemoveInvalidPooled();
            while (_instances.Count + _pool.Count < count)
            {
                if (!TryCreateInstance(out var instance))
                    return;

                _pool.Add(instance);
            }
        }

        public T Get()
        {
            T instance;
            while (_pool.Count > 0)
            {
                var index = _pool.Count - 1;
                instance = _pool[index];
                _pool.RemoveAt(index);

                if (!IsValid(instance))
                    continue;

                _instances.Add(instance);
                return instance;
            }

            if (!TryCreateInstance(out instance))
                return null;

            _instances.Add(instance);
            return instance;
        }

        public bool Release(T instance)
        {
            var index = _instances.IndexOf(instance);
            if (index < 0)
                return false;

            _instances.RemoveAt(index);
            _pool.Add(instance);
            return true;
        }

        public bool Remove(T instance)
        {
            // intentionally avoiding overloaded ==
            var index = IndexByTrueReference(_instances, instance);
            if (index >= 0)
            {
                _instances.RemoveAt(index);
                return true;
            }

            index = IndexByTrueReference(_pool, instance);
            if (index < 0)
                return false;

            _pool.RemoveAt(index);
            return true;

            int IndexByTrueReference(List<T> list, T instance)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], instance))
                        return i;
                }

                return -1;
            }
        }

        public void ReleaseAll()
        {
            _pool.AddRange(_instances);
            _instances.Clear();
        }

        private bool IsValid(T instance)
        {
            return !ReferenceEquals(instance, null) && (_isValid == null || _isValid.Invoke(instance));
        }

        private bool TryCreateInstance(out T instance)
        {
            instance = _factoryMethod.Invoke();
            if (IsValid(instance))
                return true;

            Log.LogError($"{GetType().Name} factory returned an invalid instance.");
            return false;
        }

        private void RemoveInvalidPooled()
        {
            for (var i = _pool.Count - 1; i >= 0; i--)
            {
                if (!IsValid(_pool[i]))
                    _pool.RemoveAt(i);
            }
        }
    }
}