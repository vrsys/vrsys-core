# Local Recording Test

Demonstrates recording and replay of position, rotation, scale, active state and
parent changes without starting a network session.

## Prerequisites

- Install VRSYS Recording and its VRSYS Core dependency through Package Manager.
- Use Windows x86_64 or Android arm64 for the included native recording plugin.
- Configure the project's XR runtime if using an HMD.

## Usage

1. Import **Recording Tests** from **VRSYS Recording > Samples** in Package Manager.
2. Open `Scenes/VRSYS-Recording-LocalTest.unity`.
3. Enter Play mode and select the scene's `RecorderController` component.
4. In the recorder's **Record** controls, click **Local**, let the test objects
   animate, and click **Stop recording**.
5. Select that recording in **Selected Replay File**, then click **Local** in the
   **Replay** controls. Inspect transform, active state and parent changes.

The scene uses the recording project name `VRSYS-Recording-Samples` and recording
name `LocalRecordingTest`. Check the recorder's Files settings for the recording
directory and playback name. Microphone and audio listener recording are disabled
in this scene.

The test behaviours are in the imported sample's shared `../Scripts` folder and
compile into the `vrsys.recording.samples` assembly. VRSYS Core's **Sample Scene
Setups** sample is not required for this local scene.
