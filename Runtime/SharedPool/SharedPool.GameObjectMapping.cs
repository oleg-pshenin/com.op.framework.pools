using System.Collections.Generic;
using UnityEngine;

namespace OP.Framework.Pools.SharedPool
{
    public partial class SharedPool
    {
        private readonly Dictionary<GameObject, Component> _activeInstanceByGameObject = new();
        private readonly Dictionary<Component, GameObject> _gameObjectByActiveInstance = new();

        private bool RegisterGameObjectMapping(Component instance)
        {
            var gameObject = instance.gameObject;
            if (_activeInstanceByGameObject.TryGetValue(gameObject, out var existingInstance))
            {
                Log.LogError($"GameObject '{gameObject.name}' is already registered as an active pooled instance ({existingInstance.GetType().Name}).", gameObject);
                return false;
            }

            _activeInstanceByGameObject.Add(gameObject, instance);
            _gameObjectByActiveInstance.Add(instance, gameObject);
            return true;
        }

        private void UnregisterGameObjectMapping(Component instance)
        {
            if (!_gameObjectByActiveInstance.TryGetValue(instance, out var gameObject))
                return;

            _gameObjectByActiveInstance.Remove(instance);
            _activeInstanceByGameObject.Remove(gameObject);
        }

        private void ReleaseGameObjectInternal(GameObject instance, PoolScope expectedScope = null)
        {
            if (ReferenceEquals(instance, null))
            {
                Log.LogError("Trying to release a null GameObject in SharedPool.");
                return;
            }

            if (!_activeInstanceByGameObject.TryGetValue(instance, out var pooledInstance))
            {
                Log.LogError("Trying to release a GameObject that is not active in SharedPool.", instance);
                return;
            }

            ReleaseInternal(pooledInstance, expectedScope);
        }
    }
}