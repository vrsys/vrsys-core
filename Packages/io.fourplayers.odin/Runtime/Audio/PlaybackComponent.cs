using System;
using System.Linq;
using OdinNative.Core;
using OdinNative.Core.Imports;
using OdinNative.Odin;
using OdinNative.Odin.Media;
using UnityEngine;

namespace OdinNative.Unity.Audio
{
    /// <summary>
    ///     Handles the Playback for received ODIN audio data.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlaybackComponent : MonoBehaviour
    {
        /// <summary>
        ///     THe minimum audio buffer size. I do not recommend lowering this, because values below 20ms lead to an extreme
        ///     amount of noise.
        /// </summary>
        private const float MinBufferSize = 0.02f;

        /// <summary>
        ///     The maximum audio buffer size - if we go above this, reset the audio buffer. Will lead to a bit of noise, but
        ///     reset the audio lag.
        /// </summary>
        private const float MaxBufferSize = 2f * TargetBufferSize;

        /// <summary>
        ///     The target audio buffer size in seconds.
        /// </summary>
        private const float TargetBufferSize = 0.1f;

        /// <summary>
        ///     The maximum divergence in seconds from the <see cref="TargetBufferSize" /> before starting to adjust the pitch.
        /// </summary>
        private const float TargetBufferTolerance = 0.015f;

        /// <summary>
        ///     The maximum pitch change available to move the audio buffer size back towards the <see cref="TargetBufferSize" />.
        /// </summary>
        private const float TargetSizePitchAdjustment = 0.025f;

        /// <summary>
        ///     The maximum amount of zero frames in seconds we wait before resetting the current audio buffer. Uses
        ///     the <see cref="LastPlaybackUpdateTime" /> to determine if we have hit this value.
        /// </summary>
        private const float MaxFrameLossTime = 0.2f;
        
        /// <summary>
        ///     The Unity AudioSource component for playback
        /// </summary>
        /// <remarks>Unity controls the playback device: no ConfigurationChanged event</remarks>
        public AudioSource PlaybackSource;

        /// <summary>
        ///     On true destroy the <see cref="PlaybackSource" /> in dispose to not leak
        ///     <see cref="UnityEngine.AudioSource" />
        ///     <see href="https://docs.unity3d.com/ScriptReference/AudioSource.html">(AudioSource)</see>
        ///     or false for manually manage sources
        /// </summary>
        public bool AutoDestroyAudioSource = true;

        /// <summary>
        ///     On true destroy the <see cref="OdinNative.Odin.Media.MediaStream" /> in dispose to not leak
        ///     or false for manually manage stream
        /// </summary>
        /// <remarks>On room leave/destroy the underlying streams will still be freed up</remarks>
        public bool AutoDestroyMediaStream = true;

        /// <summary>
        ///     The playback <see cref="OdinNative.Core.MediaSampleRate" />
        /// </summary>
        /// <remarks>
        ///     Set value is ignored on
        ///     <see cref="UnityEngine.AudioClip" />
        ///     <see href="https://docs.unity3d.com/ScriptReference/AudioClip.html">(AudioClip)</see>
        ///     creation if <see cref="OverrideSampleRate" /> is false
        /// </remarks>
        public MediaSampleRate SampleRate = OdinDefaults.RemoteSampleRate;

        /// <summary>
        ///     Represents the audio clip buffer used for Unity Playback. The Spatial Clip Data is set to this data every frame.
        ///     Could potentially also be filled asynchronously, if implementation is changed to async.
        /// </summary>
        private float[] _ClipBuffer;
        
        private bool _IsDestroying;
        private long _MediaStreamId;
        private ulong _PeerId;

        /// <summary>
        ///     Buffer used to read data from the media stream.
        /// </summary>
        private float[] _ReadBuffer;
        
        private string _RoomName;
        
        /// <summary>
        ///     The end position of the buffered stream audio frames inside the Spatial Audio Clip. We use this to append
        ///     a new Audio Frame from the Media Stream.
        /// </summary>
        private int _FrameBufferEndPos;

        /// <summary>
        ///     The last time we read an ODIN audio frame into the output buffer.
        /// </summary>
        private float LastPlaybackUpdateTime;

        private PlaybackStream _playbackMedia;
        private PlaybackStream PlaybackMedia
        {
            get => _playbackMedia;
            set
            {
                _playbackMedia = value;
                _playbackMedia?.AudioReset();
            }
        }

        internal bool RedirectPlaybackAudio = true;

        private AudioClip SpatialClip;

        /// <summary>
        ///     The Unity AudioSource mute property
        /// </summary>
        /// <remarks>Sets volume to 0 or restore original volume</remarks>
        public bool Mute
        {
            get => PlaybackSource?.mute ?? true;
            set
            {
                if (PlaybackSource == null) return;
                PlaybackSource.mute = value;
            }
        }

        internal PlaybackStream OdinMedia => OdinHandler.Instance.Client
            .Rooms[RoomName]?
            .RemotePeers[PeerId]?
            .Medias[MediaStreamId] as PlaybackStream;

        /// <summary>
        ///     Room name for this playback. Change this value to change the PlaybackStream by Rooms from the Client.
        /// </summary>
        /// <remarks>Invalid values will cause errors.</remarks>
        public string RoomName
        {
            get => _RoomName;
            set
            {
                _RoomName = value;
                PlaybackMedia = OdinMedia;
            }
        }

        /// <summary>
        ///     Peer id for this playback. Change this value to change the PlaybackStream by RemotePeers in the Room.
        /// </summary>
        /// <remarks>Invalid values will cause errors.</remarks>
        public ulong PeerId
        {
            get => _PeerId;
            set
            {
                _PeerId = value;
                PlaybackMedia = OdinMedia;
            }
        }

        /// <summary>
        ///     Media id for this playback. Change this value to pick a PlaybackStream by media id from peers Medias.
        /// </summary>
        /// <remarks>Invalid values will cause errors.</remarks>
        public long MediaStreamId
        {
            get => _MediaStreamId;
            set
            {
                _MediaStreamId = value;
                PlaybackMedia = OdinMedia;
            }
        }
        

        /// <summary>
        ///     Number of Samples in the <see cref="SpatialClip" /> used for playback.
        /// </summary>
        private int ClipSamples => SpatialClip.samples;

        /// <summary>
        ///     The position in samples of the current playback audio source. Used to determine the current size of the
        ///     audio buffer.
        /// </summary>
        private int CurrentClipPos => PlaybackSource.timeSamples;

        /// <summary>
        ///     Use the output settings given by unity. Most of the time this is 44100Hz
        /// </summary>
        private int OutSampleRate => AudioSettings.outputSampleRate;

        public bool HasActivity
        {
            get
            {
                if (PlaybackMedia == null)
                    return false;

                return PlaybackMedia.IsActive;
            }
        }

        private void Awake()
        {
            if (PlaybackSource == null)
                PlaybackSource = gameObject.GetComponents<AudioSource>()
                    .Where(s => s.clip == null)
                    .FirstOrDefault() ?? gameObject.AddComponent<AudioSource>();
        }

        private void Reset()
        {
            RedirectPlaybackAudio = true;
            SampleRate = OdinHandler.Config.RemoteSampleRate;
        }

        private void FixedUpdate()
        {
            bool canRead = !(_IsDestroying || PlaybackMedia == null || PlaybackMedia.HasErrors ||
                             RedirectPlaybackAudio == false);
            if (canRead)
            {
                // readBufferSize is based on the fixed unscaled delta time - we want to read "one frame" from the media stream
                int readBufferSize = Mathf.FloorToInt(Time.fixedUnscaledDeltaTime * OutSampleRate);
                if (null == _ReadBuffer || _ReadBuffer.Length != readBufferSize)
                    _ReadBuffer = new float[readBufferSize];

                // read the audio frame from the input data
                uint readResult = PlaybackMedia.AudioReadData(_ReadBuffer, readBufferSize);
                if (Utility.IsError(readResult))
                {
                    Debug.LogWarning(
                        $"{nameof(PlaybackComponent)} AudioReadData failed with error code {readResult}");
                }
                else
                {
                    // sometimes we get "zero frames" from the media stream - meaning the _readbuffer is filled entirely with zeroes
                    // We want to avoid pushing those zero frames into the play back clip buffer
                    int numZeros = 0;
                    foreach (var entry in _ReadBuffer)
                        // a float comparison with exactly 0 is slow, but in this case we have to do it, approximations
                        // will not work, because there is potentially data with very small values in the read buffer
                        if (entry == 0)
                            numZeros++;

                    // Only read the data, if there is data in the _readBuffer
                    if (numZeros != readBufferSize)
                    {
                        // write the data into the _clipBuffer.
                        for (int i = 0; i < readBufferSize; i++)
                        {
                            int writePosition = _FrameBufferEndPos + i;
                            writePosition %= ClipSamples;
                            _ClipBuffer[writePosition] = _ReadBuffer[i];
                        }

                        // Update the buffer end position
                        _FrameBufferEndPos += readBufferSize;
                        _FrameBufferEndPos %= ClipSamples;
                        // Update the last time we wrote into the playback clip buffer
                        LastPlaybackUpdateTime = Time.time;
                    }
                }
            }


            int distanceToClipStart = GetBufferDistance(CurrentClipPos, _FrameBufferEndPos);
            // The size / duration of the current audio buffer.
            float audioBufferSize = (float)distanceToClipStart / OutSampleRate;

            // Reset the frame buffering, if we haven't received an audio frame for a certain amount of time
            bool shouldResetFrameBuffer = Time.time - LastPlaybackUpdateTime > MaxFrameLossTime;
            shouldResetFrameBuffer |=
                audioBufferSize <
                MinBufferSize; // This is a fixed value - anything below this will lead to audio issues
            shouldResetFrameBuffer |= audioBufferSize > MaxBufferSize;
            if (shouldResetFrameBuffer) _FrameBufferEndPos = GetTargetFrameBufferEndPosition();

            // We'll adjust the playback source pitch to try and keep the audio buffer size close to the target
            float targetPitch = 1.0f;
            // if the audio buffer size is below the threshold, lower the pitch to allow the media stream input to catch up
            if (audioBufferSize < TargetBufferSize - TargetBufferTolerance)
                targetPitch = 1.0f - TargetSizePitchAdjustment;
            // if the audio buffer size is above the threshold, increase the pitch to allow the clip playback to catch up
            else if (audioBufferSize > TargetBufferSize + TargetBufferTolerance)
                targetPitch = 1.0f + TargetSizePitchAdjustment;

            // Interpolate the pitch over a few frames to avoid sudden pitch jumps.
            float pitch = PlaybackSource.pitch;
            pitch += (targetPitch - pitch) * 0.1f;
            PlaybackSource.pitch = pitch;

            // we also need to clean up any already played data from the clip buffer. Otherwise the playback will loop
            // once no new data is inserted
            int cleanUpCount = GetBufferDistance(_FrameBufferEndPos, CurrentClipPos);
            for (int i = 0; i < cleanUpCount; i++)
            {
                int cleanUpIndex = (_FrameBufferEndPos + i) % ClipSamples;
                _ClipBuffer[cleanUpIndex] = 0.0f;
            }

            // Debug.Log($"Audio Buffer: {audioBufferSize * 1000.0f} ms, Pitch: {pitch}, fixed delta time: {Time.fixedUnscaledDeltaTime}");
            // finally insert the read data into the spatial clip.
            SpatialClip.SetData(_ClipBuffer, 0);
        }

        /// <summary>
        ///     We don't need to resample the odin input, because Unity will automatically resample the data of an AudioClip
        ///     and output it at the chosen system sample rate
        /// </summary>
        private void OnEnable()
        {
            LastPlaybackUpdateTime = Time.time;
            if (PlaybackMedia != null && PlaybackMedia.HasErrors)
                Debug.LogWarning(
                    $"{nameof(PlaybackComponent)} on {gameObject.name} had errors in {nameof(PlaybackStream)} and should be destroyed! {PlaybackMedia}");

            RedirectPlaybackAudio = true;
            if (OdinHandler.Config.VerboseDebug)
                Debug.Log(
                    $"## {nameof(PlaybackComponent)}.OnEnable AudioSettings: outputSampleRate {AudioSettings.outputSampleRate}, driverCapabilities {Enum.GetName(typeof(AudioSpeakerMode), AudioSettings.driverCapabilities)}, speakerMode {Enum.GetName(typeof(AudioSpeakerMode), AudioSettings.speakerMode)}");


            int clipSamples = (int)(OutSampleRate * 3.0f * TargetBufferSize);
            SpatialClip = AudioClip.Create("spatialClip", clipSamples, 1, OutSampleRate, false);
            ResetAudioClip();
            PlaybackSource.clip = SpatialClip;
            PlaybackSource.loop = true;
            PlaybackSource.Play();

            _ClipBuffer = new float[ClipSamples];

            _FrameBufferEndPos = GetTargetFrameBufferEndPosition();
            _FrameBufferEndPos %= ClipSamples;
        }

        private void OnDisable()
        {
            PlaybackSource.Stop();
            RedirectPlaybackAudio = false;
        }

        private void OnDestroy()
        {
            _IsDestroying = true;

            if (AutoDestroyAudioSource)
                Destroy(PlaybackSource);

            if (AutoDestroyMediaStream)
                OdinHandler.Instance.Client?
                    .Rooms[RoomName]?
                    .RemotePeers[PeerId]?
                    .Medias.Free(MediaStreamId);
        }
        
        public NativeBindings.OdinAudioStreamStats GetOdinAudioStreamStats()
        {
            if (PlaybackMedia.AudioStats(out NativeBindings.OdinAudioStreamStats stats))
            {
                return stats;
            }

            Debug.LogError(
                $"{nameof(PlaybackComponent)} \"{gameObject.name}\" Get stats for {MediaStreamId} of peer {PeerId} in room \"{RoomName}\" failed!");
            return new NativeBindings.OdinAudioStreamStats();
        }

        /// <summary>
        ///     Returns the targeted frame buffer end position in time samples. The End position is located
        ///     <see cref="TargetBufferSize" /> ms
        ///     in front of the current playback clip position.
        /// </summary>
        /// <returns>The targeted frame buffer end position in time samples</returns>
        private int GetTargetFrameBufferEndPosition()
        {
            return (int) (CurrentClipPos + TargetBufferSize * OutSampleRate);
        }

        /// <summary>
        ///     The distance (in time samples) between two time samples on the current playback clip.
        /// </summary>
        /// <param name="a">First time sample</param>
        /// <param name="b">Second time sample</param>
        /// <returns>Distance (in time samples) between two time samples</returns>
        private int GetBufferDistance(int a, int b)
        {
            int result = b - a;
            if (result < 0)
                result += ClipSamples;
            return result;
        }

        /// <summary>
        ///     Resets the data in the <see cref="SpatialClip" />.
        /// </summary>
        private void ResetAudioClip()
        {
            SpatialClip.SetData(new float[ClipSamples], 0);
        }
    }
}