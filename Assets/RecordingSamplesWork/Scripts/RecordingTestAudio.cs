using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Plays a test clip either as one-shots (one start every <see cref="interval"/> seconds, silent in between)
    /// or as a continuous loop whose pitch changes every <see cref="interval"/> seconds, so audio recordings can be
    /// checked for timing and pitch. An optional <see cref="indicator"/> (e.g. a small "Pulse" sphere) briefly
    /// grows whenever a clip starts or the loop changes pitch; its scale is recorded like any other transform, so
    /// the replay shows when each sound should be heard.
    ///
    /// The live sound stops while a replay is prepared or running, and pauses while
    /// <see cref="RecordingTestObjectsController.runTestBehaviours"/> is off.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Audio")]
    [RequireComponent(typeof(AudioSource))]
    public class RecordingTestAudio : RecordingTestBehaviour
    {
        [Tooltip("Clip to play. It is assigned to the AudioSource on this object.")]
        public AudioClip clip;

        [Tooltip("On: play the clip as a continuous loop and change its pitch every interval. " +
                 "Off: start the clip once every interval (one-shots).")]
        public bool loop;

        [Tooltip("Seconds between one-shots, or between pitch changes of the loop.")]
        public float interval = 2f;

        [Tooltip("Pitches used in turn, one per interval.")]
        public float[] pitches = { 1f, 1.25f, 1.5f };

        [Tooltip("Optional object that briefly grows whenever a clip starts or the loop changes pitch.")]
        public Transform indicator;

        [Tooltip("Relative growth of the indicator, e.g. 0.4 = 140% of its scale right after a start.")]
        public float indicatorPulse = 0.4f;

        private const float PulseDuration = 0.5f;

        private AudioSource _source;
        private Vector3 _indicatorScale = Vector3.one;
        private float _sinceTrigger;
        private float _pulseAge = PulseDuration;
        private int _pitchIndex;
        private bool _started;
        private bool _warnedMissingClip;

        protected override GameObject Target => gameObject;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            if (clip != null)
                _source.clip = clip;
            if (indicator != null)
                _indicatorScale = indicator.localScale;
        }

        protected override void Tick(float deltaTime)
        {
            if (clip == null)
            {
                if (!_warnedMissingClip)
                {
                    _warnedMissingClip = true;
                    Debug.LogWarning($"[{nameof(RecordingTestAudio)}] No clip assigned on '{name}'.", this);
                }
                return;
            }

            if (!_started)
            {
                _started = true;
                _sinceTrigger = 0f;
                Trigger();
            }
            else
            {
                _sinceTrigger += deltaTime;
                if (interval > 0f && _sinceTrigger >= interval)
                {
                    _sinceTrigger -= interval;
                    Trigger();
                }
                else if (loop && !_source.isPlaying)
                {
                    // Something else stopped the loop (e.g. a replay used this source); continue it.
                    _source.Play();
                }
            }

            _pulseAge += deltaTime;
            ApplyIndicator();
        }

        private void Trigger()
        {
            if (_source.clip != clip)
                _source.clip = clip;

            _source.pitch = pitches != null && pitches.Length > 0 ? pitches[_pitchIndex % pitches.Length] : 1f;
            _pitchIndex++;

            if (loop)
            {
                _source.loop = true;
                if (!_source.isPlaying)
                    _source.Play();
            }
            else
            {
                // Play() rather than PlayOneShot(): the source then reports isPlaying, which the recorder relies on.
                _source.loop = false;
                _source.Play();
            }

            _pulseAge = 0f;
        }

        private void ApplyIndicator()
        {
            if (indicator == null)
                return;
            float pulse = Mathf.Clamp01(1f - _pulseAge / PulseDuration);
            indicator.localScale = _indicatorScale * (1f + indicatorPulse * pulse * pulse);
        }

        protected override void OnTestBehaviourRunningChanged(bool running)
        {
            if (_source == null || IsReplaying)
                return;
            if (running)
                _source.UnPause();
            else
                _source.Pause();
        }

        protected override void OnReplayPreparing()
        {
            // Every peer stops its own live sound so only the recorded audio is heard during the replay.
            if (_source != null)
                _source.Stop();
        }

        protected override void ResetToInitialState()
        {
            _started = false;
            _sinceTrigger = 0f;
            _pitchIndex = 0;
            _pulseAge = PulseDuration;
            if (_source != null)
            {
                _source.Stop();
                _source.pitch = pitches != null && pitches.Length > 0 ? pitches[0] : 1f;
            }
            if (indicator != null)
                indicator.localScale = _indicatorScale;
        }
    }
}
