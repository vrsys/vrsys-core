// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//-----------------------------------------------------------------
//   Authors:        Anton Lammert
//   Date:           2026
//-----------------------------------------------------------------

using UnityEngine;
using VRSYS.Meta.Avatars;

namespace VRSYS.Recording
{
    /// <summary>
    /// Optional Meta Avatar integration for the recording system. Place this component on the same
    /// GameObject as the <see cref="RecorderController"/>. It registers itself as an
    /// <see cref="IGenericRecorderProvider"/> and attaches <see cref="MetaAvatarRecorder"/>s for the
    /// Meta avatar data readers/writers in the scene, replacing the previously hard-coded Meta logic
    /// inside the core <see cref="RecorderController"/> and <see cref="ScenePreparator"/>.
    ///
    /// This lives in the optional <c>vrsys.recording.meta</c> assembly, which only compiles when the
    /// VRSYS Meta integration package is present, so the core recording package stays Meta-free.
    /// </summary>
    [RequireComponent(typeof(RecorderController))]
    public class MetaAvatarRecorderInstaller : MonoBehaviour, IGenericRecorderProvider
    {
        private RecorderController _controller;
        private ScenePreparator _scenePreparator;

        private void Awake()
        {
            _controller = GetComponent<RecorderController>();
            _scenePreparator = GetComponent<ScenePreparator>();

            // Keep the Meta replay-data writer alive when prefabs are stripped for playback.
            ReplayComponentPreserver.Preserve<MetaAvatarReplayDataWriter>();
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.RegisterGenericRecorderProvider(this);
            if (_scenePreparator != null)
                _scenePreparator.AvatarPlaybackSetup += OnAvatarPlaybackSetup;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.UnregisterGenericRecorderProvider(this);
            if (_scenePreparator != null)
                _scenePreparator.AvatarPlaybackSetup -= OnAvatarPlaybackSetup;
        }

        public void AttachGenericRecorders(RecorderController controller)
        {
            State state = controller.CurrentState;

            if (state == State.PrepareRecording)
            {
                MetaAvatarReplayDataReader[] readers =
                    FindObjectsByType<MetaAvatarReplayDataReader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (MetaAvatarReplayDataReader reader in readers)
                {
                    if (reader.GetComponent<MetaAvatarRecorder>() == null)
                        AttachRecorder(controller, reader.gameObject);
                }
            }

            if (state == State.PreparingReplay)
            {
                MetaAvatarReplayDataWriter[] writers =
                    FindObjectsByType<MetaAvatarReplayDataWriter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (MetaAvatarReplayDataWriter writer in writers)
                {
                    // Skip writers attached to a live network user; only playback users get a recorder.
                    if (writer.GetComponent<MetaAvatarHandler>() != null)
                        continue;
                    if (writer.GetComponent<MetaAvatarRecorder>() == null)
                        AttachRecorder(controller, writer.gameObject);
                }
            }
        }

        private static void AttachRecorder(RecorderController controller, GameObject target)
        {
            MetaAvatarRecorder recorder = target.AddComponent<MetaAvatarRecorder>();
            recorder.controller = controller;
            controller.RegisterRecorder(recorder.GetInstanceID(), recorder);
        }

        private void OnAvatarPlaybackSetup(GameObject newGo, string metaIDNodeString)
        {
            MetaAvatarReplayDataWriter writer = newGo.GetComponentInChildren<MetaAvatarReplayDataWriter>();
            if (writer != null)
                writer.gameObject.name = metaIDNodeString;
        }
    }
}
