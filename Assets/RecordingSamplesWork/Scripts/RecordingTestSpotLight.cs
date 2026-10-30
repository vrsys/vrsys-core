using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Animates a spot light's cone angle, color and intensity and records them as generic data, as a template for
    /// recording custom data. The component registers itself as an <see cref="IGenericRecorderProvider"/> with the
    /// <see cref="RecorderController"/>. When a recording or replay is prepared, it attaches a
    /// <see cref="RecordingTestSpotLightRecorder"/> (a <see cref="GenericRecorder"/>) to the light, which writes the
    /// cone angles, color and intensity into the generic data slots and applies them again on replay.
    /// A <see cref="RecordingTestLightCone"/> under the light makes the cone visible.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Spot Light")]
    public class RecordingTestSpotLight : RecordingTestBehaviour, IGenericRecorderProvider
    {
        [Tooltip("The animated spot light. Defaults to the Light on this object.")]
        public Light spotLight;

        [Header("Cone")]
        [Tooltip("Range of the outer cone angle in degrees (min, max).")]
        public Vector2 spotAngleRange = new Vector2(20f, 70f);

        [Tooltip("Inner cone angle relative to the outer cone angle.")]
        [Range(0f, 1f)]
        public float innerAngleRatio = 0.6f;

        [Tooltip("Cone cycles (narrow - wide - narrow) per second.")]
        public float coneFrequency = 0.25f;

        [Header("Color")]
        [Tooltip("Hue cycles per second.")]
        public float hueSpeed = 0.1f;

        [Tooltip("Color saturation.")]
        [Range(0f, 1f)]
        public float saturation = 0.75f;

        [Header("Intensity")]
        [Tooltip("Range of the light intensity (min, max).")]
        public Vector2 intensityRange = new Vector2(1.5f, 6f);

        [Tooltip("Intensity cycles (dim - bright - dim) per second. Differs from the cone frequency so that the " +
                 "three recorded values change independently.")]
        public float intensityFrequency = 0.15f;

        private float _time;

        protected override GameObject Target => spotLight != null ? spotLight.gameObject : gameObject;

        private void Awake()
        {
            if (spotLight == null)
                spotLight = GetComponent<Light>();
            Apply(0f);
        }

        protected override void Tick(float deltaTime)
        {
            _time += deltaTime;
            Apply(_time);
        }

        private void Apply(float time)
        {
            if (spotLight == null)
                return;

            float cone = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * coneFrequency * time);
            float outerAngle = Mathf.Lerp(spotAngleRange.x, spotAngleRange.y, cone);
            spotLight.spotAngle = outerAngle;
            spotLight.innerSpotAngle = outerAngle * innerAngleRatio;
            spotLight.color = Color.HSVToRGB(Mathf.Repeat(hueSpeed * time, 1f), saturation, 1f);

            float brightness = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * intensityFrequency * time);
            spotLight.intensity = Mathf.Lerp(intensityRange.x, intensityRange.y, brightness);
        }

        protected override void ResetToInitialState()
        {
            _time = 0f;
            Apply(0f);
        }

        protected override void OnControllerFound(RecorderController controller)
        {
            controller.RegisterGenericRecorderProvider(this);
        }

        protected override void OnControllerLost(RecorderController controller)
        {
            controller.UnregisterGenericRecorderProvider(this);
        }

        public void AttachGenericRecorders(RecorderController controller)
        {
            if (spotLight == null)
                return;

            GameObject lightObject = spotLight.gameObject;
            if (!controller.CoversObject(lightObject))
                return;
            var recorder = GetOrAddGenericRecorder<RecordingTestSpotLightRecorder>(lightObject, controller);
            recorder.SetId(Utils.GetObjectName(lightObject, controller.replayRoot).GetHashCode());
            recorder.RegisterRecorder();
        }
    }
}
