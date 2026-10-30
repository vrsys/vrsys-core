# Local Recording Test

Demonstrates recording and replay without starting a network session. The objects
under **Recording Test Objects** are arranged on labeled display columns in two
rows. Each column has a fixed, single-line world-space UI headline; the test
object above it can move, disappear or change parent without hiding its label.

Use **Recording Test Objects > Recording Test Objects Controller > Run Test
Behaviours** to start or pause all procedural tests together. Disable it before
replay to inspect playback without restarting live animations or test audio.

| Headline | What is recorded |
|---|---|
| Position Change | translation of the moving cube |
| Rotation Change | rotation of the cylinder |
| Scale Change | size changes of the sphere |
| Transform Changes | position, rotation and scale together |
| Activation / Deactivation | active state of `Blinking Cube` |
| Parent Change | parent changes of `Hopper` |
| Spawn / Despawn | `Spawned Cube <n>` instances that are created every 1.5 s and destroyed after 3.5 s |
| One-Shot Audio | a chime started every 2 s, silent in between |
| Continuous Audio | the same chime in a continuous loop; the pitch changes every 2 s |
| Moving Audio | a looping mono chime with an eight-second orbit and a pulsing sphere; both sound and source movement are recorded |
| Renderer Appearance | color, emission and visibility of the floating cube |
| Particle System | seed and simulation time of the soft particle fountain |
| Generic Data | cone angle, color and intensity of the spot light, shown as a light cone over its column |

## Prerequisites

- Install VRSYS Recording and its VRSYS Core dependency through Package Manager.
- Import TextMesh Pro Essential Resources if they are not already installed;
  the column headlines use its default LiberationSans SDF font.
- Use Windows x86_64 or Android arm64 for the included native recording plugin.
- Configure the project's XR runtime if using an HMD.

## Usage

1. Import **Recording Tests** from **VRSYS Recording > Samples** in Package Manager.
2. Open `Scenes/VRSYS-Recording-LocalTest.unity`.
3. Enter Play mode and select the scene's `RecorderController` component.
4. In the recorder's **Record** controls, click **Local**, let the test objects
   animate, and click **Stop recording**.
5. Select that recording in **Selected Replay File**, then click **Local** in the
   **Replay** controls. Check that:
   - transforms, active state and parent changes repeat as recorded;
   - each `Spawned Cube <n>` appears and disappears at the recorded times (the
     replay creates copies named `Spawned Cube <n>[Rec<id>]` from the prefab);
   - the chimes play at the recorded times and pitches, from the positions of the
     audio sources (each has a `Pulse` sphere that grows when its clip starts);
   - the light cone over the Generic Data column changes width, color and brightness as
     recorded, also with **Run Test Behaviours** disabled;
   - the cube's color, emission and visibility repeat, and the fountain reconstructs
     correctly when paused or scrubbed backward, including across its eight-second cycle.

The moving spatial source travels 0.55 metres left/right and 0.35 metres
forward/back around its starting position, staying above its column. Record at
least eight seconds for a complete orbit. During replay, its sphere follows the recorded path and the
recorded sound follows that position, so check the left/right direction and
distance attenuation while keeping the listener still.

The scene uses the recording project name `VRSYS-Recording-Samples` and recording
name `LocalRecordingTest`. Check the recorder's Files settings for the recording
directory and playback name. **Record all sound sources** is enabled, so every
playing AudioSource in the scene is recorded. Microphone and audio listener
recording are disabled in this scene.

## How the tests work

- All test components stop while a replay is prepared or running, and reset their
  objects when a recording starts and before a replay is prepared. The replay
  matches recorded objects by the hierarchy path they had when recording started.
- Objects created during a recording are only replayed if their prefab has a
  `RecordingPrefabInformation` that points to the prefab's location in a
  `Resources` folder. The spawned cube uses
  `../Shared/Resources/VRSYS-Recording-Tests/RecordingTestSpawnedCube.prefab` and
  the mapping in `../Shared/Prefab Information`. Every instance gets a unique name,
  and live instances are renamed and removed before a replay so they are not
  mistaken for recorded ones.
- `RecordingTestSpotLight` registers itself as an `IGenericRecorderProvider` with
  the `RecorderController`. When a recording or replay is prepared, it attaches a
  `RecordingTestSpotLightRecorder` (a `GenericRecorder`) to the light, which writes
  the cone angles, color and intensity into the generic data slots and applies
  them again on replay. Use it as a template for recording other custom data.
  The animation changes cone angle, color and intensity at different rates. The
  `Light Cone` child (`RecordingTestLightCone`) only reads the light, so the beam
  shows the live values while recording and the recorded values during replay.

The test behaviours are in the imported sample's shared `../Scripts` folder and
compile into the `vrsys.recording.samples` assembly. VRSYS Core's **Sample Scene
Setups** sample is not required for this local scene.
