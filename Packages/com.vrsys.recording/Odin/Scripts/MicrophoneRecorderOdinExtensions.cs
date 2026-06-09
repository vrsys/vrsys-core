// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//-----------------------------------------------------------------
//   Authors:        Anton Lammert
//   Date:           2026
//-----------------------------------------------------------------

using UnityEngine;
using VRSYS.Core.Chat.Odin;

namespace VRSYS.Scripts.Recording
{
    /// <summary>
    /// Optional ODIN wiring for <see cref="MicrophoneRecorder"/>. The parameterless
    /// <see cref="SetMicrophoneReader"/> locates the local <see cref="OdinMicrophoneDataAccessor"/> and
    /// swaps the recorder's microphone source to ODIN's capture stream, overriding the default Unity
    /// microphone reader that <see cref="RecorderController"/> installs during recording setup.
    ///
    /// Lives in the optional <c>vrsys.recording.odin</c> assembly, so the call site only compiles when the
    /// VRSYS ODIN chat package is present.
    /// </summary>
    public static class MicrophoneRecorderOdinExtensions
    {
        /// <summary>
        /// Routes the recorder's microphone capture through ODIN. Returns <c>true</c> when an
        /// <see cref="OdinMicrophoneDataAccessor"/> was found and wired up; <c>false</c> (leaving the
        /// existing reader in place) otherwise.
        /// </summary>
        public static bool SetMicrophoneReader(this MicrophoneRecorder recorder)
        {
            if (recorder == null)
                return false;

            OdinMicrophoneDataAccessor accessor = Object.FindFirstObjectByType<OdinMicrophoneDataAccessor>();
            if (accessor == null)
            {
                Debug.LogWarning("MicrophoneRecorder.SetMicrophoneReader: no OdinMicrophoneDataAccessor " +
                                 "found in the scene; keeping the default microphone reader.");
                return false;
            }

            recorder.SetMicrophoneReader(new OdinMicrophoneClipReader(accessor));
            return true;
        }
    }
}
