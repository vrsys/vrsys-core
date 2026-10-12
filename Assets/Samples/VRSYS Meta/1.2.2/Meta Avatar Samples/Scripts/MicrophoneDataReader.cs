using UnityEngine;
using UnityEngine.Events;

public class MicrophoneDataReader : MonoBehaviour
{
    #region Properties

    [Header("Microphone Settings")]
    public int sampleRate = 44100;
    public int recordLengthSec = 1;
    public int readBufferSize = 1024;

    private string deviceName;
    private AudioClip micClip;
    private int lastSamplePosition = 0;

    // Outputs
    private float[] audioBuffer;
    private int micPosition;

    #endregion

    #region Events

    public UnityEvent<float[], int> OnMicrophoneData = new();

    #endregion

    #region MonoBehaviour Methods

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected.");
            enabled = false;
            return;
        }

        deviceName = Microphone.devices[0];
        
        // null device = system default microphone
        audioBuffer = new float[readBufferSize];

        micClip = Microphone.Start(deviceName, true, recordLengthSec, sampleRate);

        // Wait until recording has actually started
        while (Microphone.GetPosition(deviceName) <= 0) { }
    }

    void Update()
    {
        if (micClip == null) return;

        micPosition = Microphone.GetPosition(deviceName);

        int diff = micPosition - lastSamplePosition;
        if (diff < 0) diff += micClip.samples;

        if (diff >= readBufferSize)
        {
            int startPos = micPosition - readBufferSize;
            if (startPos < 0) startPos += micClip.samples;

            micClip.GetData(audioBuffer, startPos);

            lastSamplePosition = micPosition;

            OnMicrophoneData.Invoke(audioBuffer, micPosition);
        }
    }

    void OnDisable()
    {
        if (Microphone.IsRecording(deviceName))
            Microphone.End(deviceName);
    }

    #endregion

    
}
