# VRSYS Recording

VRSYS Recording adds session recording and replay to the VRSYS framework. It records transform,
audio and generic data tracks of a networked session and replays them back, including synchronized
multi-user playback.

The package depends only on `com.vrsys.core`. Meta Avatar recording is provided as an optional,
separate assembly (`vrsys.recording.meta`) that is only compiled when the VRSYS Meta integration is
present, so the core recording functionality has no dependency on the Meta Avatar SDK.

## Native plugin

Recording and replay are backed by the native `RecordingPlugin` library, shipped in
`Runtime/Plugins` for Windows (x86_64) and Android (arm64).

## Documentation

The framework documentation can be found here: https://vrsys.gitbook.io/vrsys (still under construction)
