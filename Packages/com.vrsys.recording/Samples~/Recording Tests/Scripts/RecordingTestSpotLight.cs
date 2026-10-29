using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Animates a spot light's cone angle and color and records them as generic data, as a template for recording
    /// custom data. The component registers itself as an <see cref="IGenericRecorderProvider"/> with the
    /// <see cref="RecorderController"/>. When a recording or replay is prepared, it attaches a
    /// <see cref="RecordingTestSpotLightRecorder"/> (a <see cref="GenericRecorder"/>) to the light, which writes the
    /// cone angles, color and intensity into the generic data slots and applies them again on replay.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Spot Light")]
    public class RecordingTestSpotLight : RecordingTestBehaviour, IGenericRecorderProvider
    {
        [Tooltip("The animated spot light. Defaults to the Light on this object.")]
        public Light spotLight;

        [Tooltip("Range of the outer cone angle in degrees (min, max).")]
        public Vector2 spotAngleRange = new Vector2(20f, 70f);

        [Tooltip("Inner cone angle relative to the outer cone angle.")]
        [Range(0f, 1f)]
        public float innerAngleRatio = 0.6f;

        [Tooltip("Cone cycles (narrow - wide - narrow) per second.")]
        public float coneFrequency = 0.25f;

        [Tooltip("Hue cycles per second.")]
        public float hueSpeed = 0.1f;

        [Tooltip("Color saturation.")]
        [Range(0f, 1f)]
        public float saturation = 0.75f;

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
            var recorder = lightObject.GetComponent<RecordingTestSpotLightRecorder>();
            if (recorder == null)
                recorder = lightObject.AddComponent<RecordingTestSpotLightRecorder>();
            recorder.controller = controller;
            recorder.SetId(Utils.GetObjectName(lightObject, controller.replayRoot).GetHashCode());
            recorder.RegisterRecorder();
        }
    }
}
