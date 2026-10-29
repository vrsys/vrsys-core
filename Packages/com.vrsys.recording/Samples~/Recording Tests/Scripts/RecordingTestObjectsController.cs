using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Starts and stops all the procedural tests underneath the "Recording Test Objects" root in one go.
    ///
    /// Every <see cref="RecordingTestBehaviour"/> in the subtree finds the nearest controller and stops
    /// ticking while <see cref="runTestBehaviours"/> is off, so unchecking it pauses procedural motion,
    /// activation, reparenting, light animation and test audio without disabling the recording and replay
    /// components. Re-checking resumes the tests from their paused state. In a networked session each peer
    /// controls its own local tests; change the server's switch to pause the server-authoritative networked
    /// tests.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Objects Controller")]
    public class RecordingTestObjectsController : MonoBehaviour, IGenericRecorderProvider
    {
        [Tooltip("Runs the procedural test behaviours under this object. Uncheck to pause them while " +
                 "recording and replay keep working.")]
        public bool runTestBehaviours = true;

        private RecorderController _recorder;

        private void Awake()
        {
            // Recorder discovery must also work when the individual test behaviours are disabled.
            RegisterRecorderProvider();
        }

        private void OnEnable() { RegisterRecorderProvider(); }

        private void RegisterRecorderProvider()
        {
            if (_recorder == null)
                _recorder = FindFirstObjectByType<RecorderController>();
            if (_recorder != null)
                _recorder.RegisterGenericRecorderProvider(this);
        }

        private void OnDestroy()
        {
            if (_recorder != null)
                _recorder.UnregisterGenericRecorderProvider(this);
        }

        public void AttachGenericRecorders(RecorderController controller)
        {
            foreach (var behaviour in GetComponentsInChildren<RecordingTestBehaviour>(true))
                if (behaviour is IGenericRecorderProvider provider)
                    provider.AttachGenericRecorders(controller);
        }

        /// <summary>True while the test behaviours may tick. A disabled component counts as off.</summary>
        public bool IsRunning => enabled && runTestBehaviours;
    }
}
