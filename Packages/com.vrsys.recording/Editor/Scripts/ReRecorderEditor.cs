using UnityEditor;
using UnityEngine;

namespace VRSYS.Scripts.Recording
{
#if UNITY_EDITOR_A
    [CustomEditor(typeof(ReRecorder))]
    public class ReRecorderEditor : Editor
    {
        private float startTime;
        private bool rerecordTransforms = true;
        private bool rerecordSounds = true;
        private bool rerecordGenerics = true;

        public override void OnInspectorGUI()
        {
            ReRecorder reRecorder = (ReRecorder)target;
            RecorderState state = reRecorder.GetComponent<RecorderState>();

            GUILayout.Label("Re-Records a section of an existing replay and overwrites the\noriginal samples in the configured tracks.");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Buffering", reRecorder.IsBuffering ? "Yes" : "No");
            EditorGUILayout.LabelField("Processing", reRecorder.IsProcessing ? "Yes" : "No");
            EditorGUI.BeginChangeCheck();
            bool verbose = EditorGUILayout.Toggle("Verbose", reRecorder.verbose);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(reRecorder, "Changed ReRecorder Verbose");
                reRecorder.verbose = verbose;
                EditorUtility.SetDirty(reRecorder);
            }

            if (state != null)
            {
                EditorGUILayout.LabelField("Recorder State", state.currentState.ToString());
                if (state.currentState == State.Replaying)
                    EditorGUILayout.LabelField("Replay Time", state.currentReplayTime.ToString("F"));
            }

            EditorGUILayout.Space();
            GUILayout.Label("Targets to capture:");
            using (new EditorGUI.DisabledScope(reRecorder.IsBuffering || reRecorder.IsProcessing))
            {
                rerecordTransforms = GUILayout.Toggle(rerecordTransforms, "Transforms");
                rerecordSounds = GUILayout.Toggle(rerecordSounds, "Sounds");
                rerecordGenerics = GUILayout.Toggle(rerecordGenerics, "Generic");
            }

            EditorGUILayout.Space();

            if (!reRecorder.IsBuffering)
            {
                bool canBegin = Application.isPlaying
                                && !reRecorder.IsProcessing
                                && state != null
                                && state.currentState == State.Replaying
                                && BuildConfiguration() != ReRecordConfiguration.None;

                using (new EditorGUI.DisabledScope(!canBegin))
                {
                    if (GUILayout.Button("Begin Re-Record"))
                    {
                        ReRecordTargets targets = new ReRecordTargets
                        {
                            configuration = BuildConfiguration()
                        };
                        startTime = state.currentReplayTime;
                        reRecorder.Begin(startTime, targets);
                    }
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying || reRecorder.IsProcessing))
                {
                    if (GUILayout.Button("Undo Last Re-Record"))
                        reRecorder.Undo();

                    if (GUILayout.Button("Save Edits To Disk"))
                        reRecorder.Save();
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("End Re-Record (apply overwrites)"))
                        reRecorder.End();

                    if (GUILayout.Button("Cancel Re-Record (discard buffer)"))
                        reRecorder.Cancel();
                }
            }

            if (Application.isPlaying && (reRecorder.IsBuffering
                || reRecorder.IsProcessing
                || (state != null && state.currentState == State.Replaying)))
                Repaint();
        }

        private ReRecordConfiguration BuildConfiguration()
        {
            ReRecordConfiguration config = ReRecordConfiguration.None;
            if (rerecordTransforms) config |= ReRecordConfiguration.Transform;
            if (rerecordSounds) config |= ReRecordConfiguration.Sound;
            if (rerecordGenerics) config |= ReRecordConfiguration.Generic;
            return config;
        }
    }
#endif
}
