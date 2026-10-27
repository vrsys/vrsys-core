using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Moves an object between several parents at a fixed interval, keeping its local pose, to verify that
    /// hierarchy changes are recorded and replayed. Put it on an object that is not the target.
    /// Networked targets are reparented by the server with NetworkObject.TrySetParent; their parents then
    /// have to be spawned NetworkObjects as well.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Reparent")]
    public class RecordingTestReparent : RecordingTestBehaviour
    {
        [Tooltip("The object that changes its parent.")]
        public Transform target;

        [Tooltip("Parents the target cycles through. The target's parent at start is used first if it is in the list.")]
        public Transform[] parents = new Transform[0];

        [Tooltip("Seconds between two parent changes.")]
        public float interval = 2f;

        private Transform _startParent;
        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private Vector3 _startLocalScale;
        private int _index;
        private float _timer;

        protected override GameObject Target => target != null ? target.gameObject : null;

        private void Awake()
        {
            if (target == null)
                return;
            _startParent = target.parent;
            _startLocalPosition = target.localPosition;
            _startLocalRotation = target.localRotation;
            _startLocalScale = target.localScale;
            _index = System.Array.IndexOf(parents, _startParent);
        }

        protected override void Tick(float deltaTime)
        {
            if (target == null || parents == null || parents.Length == 0)
                return;

            _timer += deltaTime;
            if (_timer < interval)
                return;

            _timer = 0f;
            _index = (_index + 1) % parents.Length;
            SetParent(parents[_index]);
        }

        protected override void ResetToInitialState()
        {
            _timer = 0f;
            _index = System.Array.IndexOf(parents, _startParent);
            if (target != null)
                SetParent(_startParent);
        }

        private void SetParent(Transform parent)
        {
            if (target.parent != parent)
            {
                var networkObject = SpawnedNetworkObject;
                if (networkObject != null)
                    networkObject.TrySetParent(parent, false);
                else
                    target.SetParent(parent, false);
            }

            target.localPosition = _startLocalPosition;
            target.localRotation = _startLocalRotation;
            target.localScale = _startLocalScale;
        }
    }
}
