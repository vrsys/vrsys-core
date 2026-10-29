using UnityEngine;

namespace VRSYS.Recording.Testing
{
    [RequireComponent(typeof(ParticleSystem))]
    public class RecordingTestParticles : RecordingTestBehaviour, IGenericRecorderProvider
    {
        public const float CycleDuration = 8f;
        public uint seed = 7319;
        public float SimulationTime { get; private set; }
        private ParticleSystem _particles;
        protected override GameObject Target => gameObject;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            ResetToInitialState();
        }

        protected override void Tick(float deltaTime)
        {
            SimulationTime += deltaTime;
            AnimateProperties(SimulationTime);
            SimulateAt(_particles, SimulationTime, seed);
        }

        private void AnimateProperties(float time)
        {
            float phase = Mathf.PI * 2f * time / CycleDuration;
            float swell = 0.5f - 0.5f * Mathf.Cos(phase);
            var main = _particles.main;
            main.startSpeed = Mathf.Lerp(0.65f, 1.25f, swell);
            main.startLifetime = Mathf.Lerp(1.1f, 1.75f, swell);
            main.startSize = Mathf.Lerp(0.035f, 0.085f, 0.5f + 0.5f * Mathf.Sin(phase));
            main.gravityModifier = Mathf.Lerp(0.025f, 0.1f, swell);
            main.startRotation = 0.4f * Mathf.Sin(phase);
            main.startColor = Color.Lerp(new Color(0.12f, 0.95f, 0.96f),
                new Color(0.85f, 0.35f, 0.75f), swell);
            var emission = _particles.emission;
            emission.enabled = time % CycleDuration < 7.2f;
            emission.rateOverTime = Mathf.Lerp(24f, 64f, swell);
            var shape = _particles.shape;
            shape.enabled = true;
            shape.angle = Mathf.Lerp(8f, 24f, swell);
            shape.radius = Mathf.Lerp(0.08f, 0.2f, swell);
        }

        // Rebuild using this sample's current settings, so seeking never depends on earlier frames.
        public static void SimulateAt(ParticleSystem particles, float time, uint randomSeed)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = randomSeed;
            particles.Simulate(Mathf.Repeat(time, CycleDuration), true, true, true);
        }

        protected override void ResetToInitialState()
        {
            SimulationTime = 0f;
            AnimateProperties(0f);
            SimulateAt(_particles, 0f, seed);
        }

        protected override void OnControllerFound(RecorderController controller) { controller.RegisterGenericRecorderProvider(this); }
        protected override void OnControllerLost(RecorderController controller) { controller.UnregisterGenericRecorderProvider(this); }
        public void AttachGenericRecorders(RecorderController controller)
        {
            var recorder = GetComponent<RecordingTestParticlesRecorder>();
            if (recorder == null) recorder = gameObject.AddComponent<RecordingTestParticlesRecorder>();
            recorder.controller = controller;
            recorder.SetId(Utils.GetObjectName(gameObject, controller.replayRoot).GetHashCode());
            recorder.RegisterRecorder();
        }
    }
}
