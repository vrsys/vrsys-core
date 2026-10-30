using Unity.Netcode.Components;
using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Hovers and spins at one of two spots and teleports between them every <see cref="teleportInterval"/> seconds,
    /// to check that teleports of networked objects are replayed as teleports: clients must see the object jump, not
    /// slide (interpolate) between the spots. The spots are more than 1 m apart, the distance from which the recorder
    /// and the replay treat a change as a teleport. With a <see cref="NetworkTransform"/>, the server teleports through
    /// it, so clients also see the jump live.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Teleport")]
    public class RecordingTestTeleport : RecordingTestBehaviour
    {
        [Tooltip("Offset of the second spot from the start position (local space). Keep it longer than 1 m.")]
        public Vector3 teleportOffset = new Vector3(0.5f, 1f, 0f);

        [Tooltip("Seconds between two teleports.")]
        public float teleportInterval = 2f;

        [Tooltip("Degrees per second around the local up axis.")]
        public float spinSpeed = 120f;

        [Tooltip("Height of the hover motion in metres.")]
        public float hoverAmplitude = 0.04f;

        [Tooltip("Hover cycles per second.")]
        public float hoverFrequency = 0.8f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private NetworkTransform _networkTransform;
        private float _time;
        private int _spot;

        protected override GameObject Target => gameObject;

        private void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
            _networkTransform = GetComponent<NetworkTransform>();
        }

        protected override void Tick(float deltaTime)
        {
            _time += deltaTime;
            int spot = teleportInterval > 0f ? Mathf.FloorToInt(_time / teleportInterval) % 2 : 0;
            float hover = hoverAmplitude * Mathf.Sin(2f * Mathf.PI * hoverFrequency * _time);
            Vector3 position = _startPosition + (spot == 1 ? teleportOffset : Vector3.zero) + Vector3.up * hover;
            Quaternion rotation = _startRotation * Quaternion.Euler(0f, spinSpeed * _time, 0f);

            if (spot != _spot)
            {
                _spot = spot;
                MoveTo(position, rotation, true);
            }
            else
            {
                MoveTo(position, rotation, false);
            }
        }

        private void MoveTo(Vector3 localPosition, Quaternion localRotation, bool teleport)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;

            if (!teleport || _networkTransform == null || !_networkTransform.IsSpawned ||
                !_networkTransform.CanCommitToTransform)
                return;

            bool local = _networkTransform.InLocalSpace;
            _networkTransform.Teleport(local ? transform.localPosition : transform.position,
                local ? transform.localRotation : transform.rotation, transform.localScale);
        }

        protected override void ResetToInitialState()
        {
            _time = 0f;
            _spot = 0;
            MoveTo(_startPosition, _startRotation, true);
        }
    }
}
