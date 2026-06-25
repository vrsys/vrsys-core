namespace VRSYS.Recording
{
    /// <summary>
    /// Extension point that lets external assemblies (e.g. the optional Meta Avatar integration)
    /// attach their own <see cref="GenericRecorder"/> subclasses without the core
    /// <see cref="RecorderController"/> having to reference them.
    ///
    /// Register an implementation on the same GameObject as the <see cref="RecorderController"/>
    /// (or via <see cref="RecorderController.RegisterGenericRecorderProvider"/>). It is invoked
    /// whenever the controller prepares a recording or a replay, in place of the previously
    /// hard-coded Meta attachment logic.
    /// </summary>
    public interface IGenericRecorderProvider
    {
        /// <summary>
        /// Attach and register any generic recorders required for the controller's current state
        /// (<see cref="RecorderController.CurrentState"/>). Implementations should add their
        /// <see cref="GenericRecorder"/> components, set <see cref="Recorder.controller"/> and call
        /// <see cref="RecorderController.RegisterRecorder"/>.
        /// </summary>
        void AttachGenericRecorders(RecorderController controller);
    }
}
