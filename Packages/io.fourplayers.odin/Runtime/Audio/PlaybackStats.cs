using UnityEngine;

namespace OdinNative.Unity.Audio
{
    public class PlaybackStats : MonoBehaviour
    {
        public bool Log = false;
        public bool PauseAnimationCurve = true;

        private PlaybackComponent currentPlayback;
        private Core.Imports.NativeBindings.OdinAudioStreamStats currentStats;

        public uint PacketsProcessed;
        public uint PacketsDroppedEarly;
        public uint PacketsDroppedLate;
        public uint PacketsLost;
        public AnimationCurve PacketsAvailableDifference;

        void OnEnable()
        {
            currentStats = new Core.Imports.NativeBindings.OdinAudioStreamStats();
            currentPlayback = GetComponent<Audio.PlaybackComponent>();
        }

        private void Update()
        {
            if (Application.isEditor == false || currentPlayback == null) return;

            currentStats = currentPlayback.GetOdinAudioStreamStats();
            PacketsProcessed = currentStats.jitter_packets_processed;
            PacketsDroppedEarly = currentStats.jitter_packets_dropped_too_early;
            PacketsDroppedLate = currentStats.jitter_packets_dropped_too_late;
            PacketsLost = currentStats.jitter_packets_lost;
            var failed = PacketsDroppedEarly + PacketsDroppedLate + PacketsLost;
            if (failed > 0 && !PauseAnimationCurve)
            {
                PacketsAvailableDifference.AddKey(Time.time, (PacketsProcessed - failed) / ((PacketsProcessed + failed) / 2f) - 1f);
                PacketsAvailableDifference.Evaluate(Time.time);
            }

            if (Log) Debug.Log($"{Time.time} {nameof(Audio.PlaybackComponent)} {currentPlayback.gameObject.name} (id {currentPlayback.MediaStreamId}):" +
                $"{nameof(PacketsProcessed)} {PacketsProcessed}," +
                $"{nameof(PacketsDroppedEarly)} {PacketsDroppedEarly}," +
                $"{nameof(PacketsDroppedLate)} {PacketsDroppedLate}," +
                $"{nameof(PacketsLost)} {PacketsLost}");
        }
    }
}
