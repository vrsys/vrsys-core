namespace VRSYS.Recording
{
    /// <summary>
    /// Marker implemented by a component that edits/overwrites the recording during replay (e.g. the
    /// app-side ReRecorder). When such a component is present on the <see cref="RecorderController"/>'s
    /// GameObject, the recording file is opened in editable mode when a replay is started.
    ///
    /// This keeps the core recording package decoupled from the (app-side) re-recording implementation.
    /// </summary>
    public interface IRecordingEditor
    {
    }
}
