using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Switches another object on and off at a fixed interval, to verify that active-state changes are
    /// recorded and replayed. Put it on an object that stays active (e.g. the parent of the target),
    /// since a component on the toggled object would stop running once the object is deactivated.
    /// Intended for local objects: the active state of a NetworkObject is not synced by Netcode.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Active Toggle")]
    public class RecordingTestActiveToggle : RecordingTestBehaviour
    {
        [Tooltip("The object to switch on and off. Must not be this object.")]
        public GameObject target;

        [Tooltip("Seconds between two switches.")]
        public float interval = 1.5f;

        private bool _startActive;
        private float _timer;

        protected override GameObject Target => target;

        private void Awake()
        {
            if (target != null)
                _startActive = target.activeSelf;
        }

        private void Start()
        {
            if (target == gameObject)
                RecordingLogger.LogWarning(nameof(RecordingTestActiveToggle), name + ": RecordingTestActiveToggle must not toggle its own GameObject.", this);
            else if (target != null && target.GetComponent<NetworkObject>() != null)
                RecordingLogger.LogWarning(nameof(RecordingTestActiveToggle), name + ": the active state of a NetworkObject is not synced by Netcode; " +
                                 "use RecordingTestActiveToggle on local objects.", this);
        }

        protected override void Tick(float deltaTime)
        {
            if (target == null || target == gameObject)
                return;

            _timer += deltaTime;
            if (_timer < interval)
                return;

            _timer = 0f;
            target.SetActive(!target.activeSelf);
        }

        protected override void ResetToInitialState()
        {
            _timer = 0f;
            if (target != null && target != gameObject)
                target.SetActive(_startActive);
        }
    }
}
