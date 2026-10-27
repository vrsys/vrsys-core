using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Moves, rotates and/or scales this object with smooth periodic motion, to verify that transform
    /// recordings replay correctly. Works on local and networked objects (networked: server only, synced by
    /// a NetworkTransform on the same object).
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Motion")]
    public class RecordingTestMotion : RecordingTestBehaviour
    {
        [Header("Position")]
        public bool animatePosition = true;
        [Tooltip("Peak offset from the start position per axis (local space).")]
        public Vector3 positionAmplitude = new Vector3(1f, 0.25f, 0f);
        [Tooltip("Cycles per second.")]
        public float positionFrequency = 0.2f;

        [Header("Rotation")]
        public bool animateRotation = true;
        [Tooltip("Degrees per second around each local axis.")]
        public Vector3 rotationSpeed = new Vector3(0f, 45f, 0f);

        [Header("Scale")]
        public bool animateScale;
        [Tooltip("Relative scale change, e.g. 0.3 = between 70% and 130% of the start scale.")]
        public float scaleAmplitude = 0.3f;
        [Tooltip("Cycles per second.")]
        public float scaleFrequency = 0.4f;

        [Tooltip("Offset of this object's cycle, so several objects don't move in lockstep.")]
        public float phase;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _startScale;
        private float _time;

        protected override GameObject Target => gameObject;

        private void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
            _startScale = transform.localScale;
        }

        protected override void Tick(float deltaTime)
        {
            _time += deltaTime;
            float angle = 2f * Mathf.PI * (positionFrequency * _time + phase);

            if (animatePosition)
            {
                // x: sine, y: twice as fast (figure eight), z: cosine.
                Vector3 offset = new Vector3(
                    positionAmplitude.x * Mathf.Sin(angle),
                    positionAmplitude.y * Mathf.Sin(2f * angle),
                    positionAmplitude.z * Mathf.Cos(angle));
                transform.localPosition = _startPosition + offset;
            }

            if (animateRotation)
                transform.localRotation = _startRotation * Quaternion.Euler(rotationSpeed * _time);

            if (animateScale)
            {
                float factor = 1f + scaleAmplitude * Mathf.Sin(2f * Mathf.PI * (scaleFrequency * _time + phase));
                transform.localScale = _startScale * factor;
            }
        }

        protected override void ResetToInitialState()
        {
            _time = 0f;
            transform.localPosition = _startPosition;
            transform.localRotation = _startRotation;
            transform.localScale = _startScale;
        }
    }
}
