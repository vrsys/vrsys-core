using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Records a spot light's cone angles, color and intensity as generic data and applies them on replay.
    /// Attached at runtime by <see cref="RecordingTestSpotLight"/>. The light's values from before the replay are
    /// restored when the replay ends.
    /// </summary>
    [AddComponentMenu("")]
    public class RecordingTestSpotLightRecorder : GenericRecorder
    {
        private Light _light;

        private bool _hasValuesBeforeReplay;
        private float _spotAngleBeforeReplay;
        private float _innerSpotAngleBeforeReplay;
        private Color _colorBeforeReplay;
        private float _intensityBeforeReplay;

        public override void Start()
        {
            _light = GetComponent<Light>();
            base.Start();
        }

        public override void OnRecordingStart()
        {
            RegisterDescription("Spot light v1: float[0]=outer spot angle; float[1]=inner spot angle; " +
                                "float[2..5]=color RGBA; float[6]=intensity.");
        }

        protected override bool FillGenericData()
        {
            if (_light == null)
                _light = GetComponent<Light>();
            if (_light == null)
                return false;

            Color color = _light.color;
            _recFloatDTO[0] = _light.spotAngle;
            _recFloatDTO[1] = _light.innerSpotAngle;
            _recFloatDTO[2] = color.r;
            _recFloatDTO[3] = color.g;
            _recFloatDTO[4] = color.b;
            _recFloatDTO[5] = color.a;
            _recFloatDTO[6] = _light.intensity;
            return true;
        }

        public override void OnReplayStart()
        {
            _light = GetComponent<Light>();
            if (_light == null)
                return;

            _spotAngleBeforeReplay = _light.spotAngle;
            _innerSpotAngleBeforeReplay = _light.innerSpotAngle;
            _colorBeforeReplay = _light.color;
            _intensityBeforeReplay = _light.intensity;
            _hasValuesBeforeReplay = true;
        }

        protected override void ProcessReplayData(float replayTime)
        {
            if (_light == null)
                return;

            _light.spotAngle = _replayFloatDTO[0];
            _light.innerSpotAngle = _replayFloatDTO[1];
            _light.color = new Color(_replayFloatDTO[2], _replayFloatDTO[3], _replayFloatDTO[4], _replayFloatDTO[5]);
            _light.intensity = _replayFloatDTO[6];
        }

        public override void OnReplayEnd()
        {
            if (_light == null || !_hasValuesBeforeReplay)
                return;

            _light.spotAngle = _spotAngleBeforeReplay;
            _light.innerSpotAngle = _innerSpotAngleBeforeReplay;
            _light.color = _colorBeforeReplay;
            _light.intensity = _intensityBeforeReplay;
            _hasValuesBeforeReplay = false;
        }
    }
}
