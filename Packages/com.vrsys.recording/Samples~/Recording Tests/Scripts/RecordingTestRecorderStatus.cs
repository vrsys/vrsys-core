using System.Text;
using TMPro;
using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// World-space status line of one recorder for the multi-recorder sample: its name, which objects it covers, its
    /// state, the file it records or replays and the current time. Helps to follow several recorders at once.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Recorder Status")]
    public class RecordingTestRecorderStatus : MonoBehaviour
    {
        [Tooltip("The recorder to show.")]
        public RecorderController recorder;

        [Tooltip("Headline, e.g. the recorder's name and the objects it covers.")]
        public string title = "Recorder";

        [Tooltip("Text that shows the status. Defaults to the TMP text on this object or its children.")]
        public TMP_Text text;

        public Color idleColor = new Color(0.78f, 0.84f, 0.9f);
        public Color recordingColor = new Color(1f, 0.45f, 0.38f);
        public Color replayingColor = new Color(0.35f, 0.9f, 0.85f);

        private readonly StringBuilder _builder = new StringBuilder();
        private string _last;

        private void Awake()
        {
            if (text == null)
                text = GetComponentInChildren<TMP_Text>(true);
        }

        private void Update()
        {
            if (text == null)
                return;

            RecorderState state = recorder != null ? recorder.recorderState : null;
            _builder.Clear();
            _builder.Append("<b>").Append(title).Append("</b>\n");
            Color color = idleColor;
            if (state == null)
            {
                _builder.Append("no recorder");
            }
            else
            {
                switch (state.currentState)
                {
                    case State.PrepareRecording:
                    case State.Recording:
                        color = recordingColor;
                        _builder.Append("Recording ").Append(state.recordingFile).Append("  ")
                            .Append(FormatTime(state.currentRecordingTime));
                        break;
                    case State.PreparingReplay:
                        color = replayingColor;
                        _builder.Append("Preparing ").Append(state.selectedReplayFile);
                        break;
                    case State.Replaying:
                        color = replayingColor;
                        _builder.Append(state.replayPaused ? "Paused " : "Replaying ").Append(state.selectedReplayFile)
                            .Append("  ").Append(FormatTime(state.currentReplayTime)).Append(" / ")
                            .Append(FormatTime(state.recordingDuration));
                        break;
                    default:
                        _builder.Append(state.currentState);
                        break;
                }
            }

            string value = _builder.ToString();
            if (value != _last)
            {
                _last = value;
                text.text = value;
            }
            text.color = color;
        }

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            return minutes.ToString("00") + ":" + (seconds - minutes * 60f).ToString("00.0");
        }
    }
}
