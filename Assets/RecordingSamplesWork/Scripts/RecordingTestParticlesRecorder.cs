using UnityEngine;

namespace VRSYS.Recording.Testing
{
    // shortcut: animated scalar settings and one Color32 in local space; arbitrary curves and collisions need additional state.
    public class RecordingTestParticlesRecorder : GenericRecorder
    {
        private ParticleSystem _particles;
        private RecordingTestParticles _driver;
        private float _beforeReplay;
        private uint _beforeSeed;
        private bool _beforeAutoSeed, _beforeEmission, _beforeShape, _beforeVisible;
        private ParticleSystem.MinMaxCurve _beforeRate, _beforeSpeed, _beforeLifetime;
        private ParticleSystem.MinMaxCurve _beforeSize, _beforeGravity, _beforeRotation;
        private ParticleSystem.MinMaxGradient _beforeColor;
        private float _beforeAngle, _beforeRadius;

        public override void Start()
        {
            _particles = GetComponent<ParticleSystem>();
            _driver = GetComponent<RecordingTestParticles>();
            base.Start();
        }

        public override void OnRecordingStart()
        {
            RegisterDescription("ParticleSystem v2: int[0]=seed, int[1]=schema version (2), int[2..4]=emission/shape/renderer enabled; " +
                "float[0..8]=elapsed seconds, emission rate, start speed, lifetime, size, gravity modifier, cone angle, radius, start rotation (radians); " +
                "char[0..3]=start RGBA (Color32). Scalar settings only; local space; eight-second reconstruction cycle.");
        }

        protected override bool FillGenericData()
        {
            if (_particles == null || _driver == null) return false;
            var main = _particles.main;
            var emission = _particles.emission;
            var shape = _particles.shape;
            Color32 color = main.startColor.color;
            _recIntDTO[0] = unchecked((int)_particles.randomSeed);
            _recIntDTO[1] = 2;
            _recIntDTO[2] = emission.enabled ? 1 : 0;
            _recIntDTO[3] = shape.enabled ? 1 : 0;
            _recIntDTO[4] = _particles.GetComponent<ParticleSystemRenderer>().enabled ? 1 : 0;
            _recFloatDTO[0] = _driver.SimulationTime;
            _recFloatDTO[1] = emission.rateOverTime.constant;
            _recFloatDTO[2] = main.startSpeed.constant;
            _recFloatDTO[3] = main.startLifetime.constant;
            _recFloatDTO[4] = main.startSize.constant;
            _recFloatDTO[5] = main.gravityModifier.constant;
            _recFloatDTO[6] = shape.angle;
            _recFloatDTO[7] = shape.radius;
            _recFloatDTO[8] = main.startRotation.constant;
            _recCharDTO[0] = color.r; _recCharDTO[1] = color.g;
            _recCharDTO[2] = color.b; _recCharDTO[3] = color.a;
            return true;
        }

        public override void OnReplayStart()
        {
            _particles = GetComponent<ParticleSystem>();
            _driver = GetComponent<RecordingTestParticles>();
            _beforeReplay = _driver != null ? _driver.SimulationTime : 0f;
            _beforeSeed = _particles.randomSeed;
            _beforeAutoSeed = _particles.useAutoRandomSeed;
            var main = _particles.main;
            _beforeSpeed = main.startSpeed; _beforeLifetime = main.startLifetime;
            _beforeSize = main.startSize; _beforeGravity = main.gravityModifier;
            _beforeRotation = main.startRotation; _beforeColor = main.startColor;
            _beforeRate = _particles.emission.rateOverTime;
            _beforeEmission = _particles.emission.enabled;
            _beforeShape = _particles.shape.enabled;
            _beforeAngle = _particles.shape.angle; _beforeRadius = _particles.shape.radius;
            _beforeVisible = _particles.GetComponent<ParticleSystemRenderer>().enabled;
        }

        protected override void ProcessReplayData(float replayTime)
        {
            // v1 recordings contain only time and seed; keep their prefab settings intact.
            if (_replayIntDTO[1] == 2)
            {
                var main = _particles.main;
                main.startSpeed = _replayFloatDTO[2];
                main.startLifetime = _replayFloatDTO[3];
                main.startSize = _replayFloatDTO[4];
                main.gravityModifier = _replayFloatDTO[5];
                main.startRotation = _replayFloatDTO[8];
                main.startColor = (Color)new Color32(_replayCharDTO[0], _replayCharDTO[1], _replayCharDTO[2], _replayCharDTO[3]);
                var emission = _particles.emission;
                emission.rateOverTime = _replayFloatDTO[1];
                emission.enabled = _replayIntDTO[2] != 0;
                var shape = _particles.shape;
                shape.enabled = _replayIntDTO[3] != 0;
                shape.angle = _replayFloatDTO[6];
                shape.radius = _replayFloatDTO[7];
                _particles.GetComponent<ParticleSystemRenderer>().enabled = _replayIntDTO[4] != 0;
            }
            RecordingTestParticles.SimulateAt(_particles, Mathf.Max(_replayFloatDTO[0], 0f), unchecked((uint)_replayIntDTO[0]));
        }

        public override void OnReplayEnd()
        {
            var main = _particles.main;
            main.startSpeed = _beforeSpeed; main.startLifetime = _beforeLifetime;
            main.startSize = _beforeSize; main.gravityModifier = _beforeGravity;
            main.startRotation = _beforeRotation; main.startColor = _beforeColor;
            var emission = _particles.emission;
            emission.rateOverTime = _beforeRate; emission.enabled = _beforeEmission;
            var shape = _particles.shape;
            shape.enabled = _beforeShape; shape.angle = _beforeAngle; shape.radius = _beforeRadius;
            _particles.GetComponent<ParticleSystemRenderer>().enabled = _beforeVisible;
            RecordingTestParticles.SimulateAt(_particles, _beforeReplay, _beforeSeed);
            _particles.useAutoRandomSeed = _beforeAutoSeed;
        }
    }
}
