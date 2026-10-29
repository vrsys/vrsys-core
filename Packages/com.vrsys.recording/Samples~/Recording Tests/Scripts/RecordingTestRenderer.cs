using UnityEngine;

namespace VRSYS.Recording.Testing
{
    [RequireComponent(typeof(MeshRenderer))]
    public class RecordingTestRenderer : RecordingTestBehaviour, IGenericRecorderProvider
    {
        public Color teal = new Color(0.06f, 0.8f, 0.86f);
        public Color coral = new Color(1f, 0.24f, 0.32f);
        private float _time;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        protected override GameObject Target => gameObject;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            Apply(0f);
        }
        protected override void Tick(float deltaTime) { _time += deltaTime; Apply(_time); }
        private void Apply(float time)
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_properties == null) _properties = new MaterialPropertyBlock();
            Color color = Color.Lerp(teal, coral, 0.5f - 0.5f * Mathf.Cos(time * Mathf.PI / 3f));
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor("_BaseColor", color);
            _properties.SetColor("_Color", color);
            _properties.SetColor("_EmissionColor", color * (0.15f + 0.45f * (0.5f + 0.5f * Mathf.Sin(time * 2f))));
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = time % 8f < 7.4f;
        }
        protected override void ResetToInitialState() { _time = 0f; Apply(0f); }
        protected override void OnControllerFound(RecorderController controller) { controller.RegisterGenericRecorderProvider(this); }
        protected override void OnControllerLost(RecorderController controller) { controller.UnregisterGenericRecorderProvider(this); }
        public void AttachGenericRecorders(RecorderController controller)
        {
            var recorder = GetComponent<RecordingTestRendererRecorder>();
            if (recorder == null) recorder = gameObject.AddComponent<RecordingTestRendererRecorder>();
            recorder.controller = controller;
            recorder.SetId(Utils.GetObjectName(gameObject, controller.replayRoot).GetHashCode());
            recorder.RegisterRecorder();
        }
    }
}
