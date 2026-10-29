using UnityEngine;

namespace VRSYS.Recording.Testing
{
    // One material slot; the shared material is never modified.
    public class RecordingTestRendererRecorder : GenericRecorder
    {
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private MaterialPropertyBlock _beforeReplay;
        private bool _wasEnabled;
        public override void Start() { _renderer = GetComponent<MeshRenderer>(); _properties = new MaterialPropertyBlock(); base.Start(); }
        public override void OnRecordingStart()
        {
            RegisterDescription("MeshRenderer v1: int[0]=enabled; float[0..3]=base RGBA; float[4..7]=emission RGBA. One material slot.");
        }
        protected override bool FillGenericData()
        {
            if (_renderer == null) return false;
            _renderer.GetPropertyBlock(_properties);
            Color color = _properties.HasColor("_BaseColor") ? _properties.GetColor("_BaseColor") : _renderer.sharedMaterial.GetColor("_BaseColor");
            Color emission = _properties.HasColor("_EmissionColor") ? _properties.GetColor("_EmissionColor") : Color.black;
            _recIntDTO[0] = _renderer.enabled ? 1 : 0;
            _recFloatDTO[0] = color.r; _recFloatDTO[1] = color.g; _recFloatDTO[2] = color.b; _recFloatDTO[3] = color.a;
            _recFloatDTO[4] = emission.r; _recFloatDTO[5] = emission.g; _recFloatDTO[6] = emission.b; _recFloatDTO[7] = emission.a;
            return true;
        }
        public override void OnReplayStart()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            _beforeReplay = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_beforeReplay);
            _wasEnabled = _renderer.enabled;
        }
        protected override void ProcessReplayData(float replayTime)
        {
            _renderer.GetPropertyBlock(_properties);
            Color color = new Color(_replayFloatDTO[0], _replayFloatDTO[1], _replayFloatDTO[2], _replayFloatDTO[3]);
            _properties.SetColor("_BaseColor", color);
            _properties.SetColor("_Color", color);
            _properties.SetColor("_EmissionColor", new Color(_replayFloatDTO[4], _replayFloatDTO[5], _replayFloatDTO[6], _replayFloatDTO[7]));
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = _replayIntDTO[0] != 0;
        }
        public override void OnReplayEnd()
        {
            if (_renderer == null || _beforeReplay == null) return;
            _renderer.SetPropertyBlock(_beforeReplay);
            _renderer.enabled = _wasEnabled;
        }
    }
}
