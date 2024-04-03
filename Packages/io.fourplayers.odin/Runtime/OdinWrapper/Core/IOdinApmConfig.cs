using OdinNative.Core.Imports;

namespace OdinNative.Core
{
    /// <summary>
    /// Interface for Audio processing configuration of an ODIN room
    /// </summary>
    public interface IOdinApmConfig
    {
        /// <summary>
        /// Enable or disable echo cancellation
        /// </summary>
        bool EchoCanceller { get; set; }
        /// <summary>
        /// Enable or disable high pass filtering
        /// </summary>
        bool HighPassFilter { get; set; }
        /// <summary>
        /// Set the aggressiveness of the suppression
        /// </summary>
        NativeBindings.OdinNoiseSuppressionLevel NoiseSuppressionLevel { get; set; }
        /// <summary>
        /// Enable or disable the pre amplifier
        /// </summary>
        bool PreAmplifier { get; set; }
        /// <summary>
        /// Enable or disable the transient suppressor
        /// </summary>
        bool TransientSuppressor { get; set; }
        /// <summary>
        /// Enables or disables voice activity detection (VAD)
        /// </summary>
        bool VoiceActivityDetection { get; set; }
        /// <summary>
        /// Voice probability value when the VAD should engage.
        /// </summary>
        float VoiceActivityDetectionAttackProbability { get; set; }
        /// <summary>
        /// Voice probability value when the VAD should disengage after previously being engaged.
        /// </summary>
        float VoiceActivityDetectionReleaseProbability { get; set; }
        /// <summary>
        /// Enables or disables volume gate
        /// </summary>
        bool VolumeGate { get; set; }
        /// <summary>
        /// Root mean square power (dBFS) when the volume gate should engage.
        /// </summary>
        float VolumeGateAttackLoudness { get; set; }
        /// <summary>
        /// Root mean square power (dBFS) when the volume gate should disengage after previously being engaged.
        /// </summary>
        float VolumeGateReleaseLoudness { get; set; }
        /// <summary>
        /// Enable or disable the gain controller
        /// </summary>
        bool GainController { get; set; }
    }
}