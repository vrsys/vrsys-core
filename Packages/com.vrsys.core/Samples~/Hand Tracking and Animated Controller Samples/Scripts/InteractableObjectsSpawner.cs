using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Core.Samples
{
    public class InteractableObjectsSpawner : NetworkBehaviour
    {
        #region Properties

        [SerializeField] private List<GameObject> _spawnableObjects;

        [SerializeField] private Vector2 _minMaxXRange = new Vector2(-5, 5);
        [SerializeField] private Vector2 _minMaxYRange = new Vector2(1, 10);
        [SerializeField] private Vector2 _minMaxZRange = new Vector2(-5, 5);

        #endregion
        
        #region RPCs

        [Rpc(SendTo.Server)]
        public void SpawnObjectRpc()
        {
            Vector3 position = new Vector3(
                Random.Range(_minMaxXRange.x, _minMaxYRange.y),
                Random.Range(_minMaxYRange.x, _minMaxYRange.y),
                Random.Range(_minMaxZRange.x, _minMaxZRange.y));

            int objectIdx = Random.Range(0, _spawnableObjects.Count);

            GameObject newObject = Instantiate(_spawnableObjects[objectIdx], position, Quaternion.identity);
            newObject.GetComponent<NetworkObject>().Spawn();
        }

        #endregion
    }
}
