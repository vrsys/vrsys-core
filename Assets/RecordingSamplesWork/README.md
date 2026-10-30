# Recording Tests

Import **Recording Tests** from **VRSYS Recording > Samples** in Package Manager.
One import includes the test scenes and the shared scripts and assets they use:

- `Local Recording Test/Scenes/VRSYS-Recording-LocalTest.unity`: local recording
  and replay of transforms, active state, hierarchy changes, objects created and
  destroyed during the recording, sound (including a moving spatial loop) and
  generic data (a spot light, renderer appearance and a seeded particle fountain).
- `Networked Recording Test/Scenes/VRSYS-Recording-NetworkedTest.unity`: the same
  tests in a networked session, plus networked objects (including ones that are
  spawned and despawned) and recordable user prefabs.
- `Multi Recorder Test/Scenes/VRSYS-Recording-MultiRecorderTest.unity`: three
  recorders with their own recording roots in one local scene: simultaneous
  recordings and replays of different object groups, a replay next to live
  objects, and a recording of a running replay.
- `Scripts`: the test behaviours in the `vrsys.recording.samples` assembly. These
  scripts are sample content and are only compiled into a consuming project when
  this sample is imported.
  - `RecordingTestObjectsController`: a **Run Test Behaviours** checkbox on the
    **Recording Test Objects** root starts/stops the procedural tests together.
  - `RecordingTestBehaviour`: base class that pauses the tests during replay and
    resets them when a recording starts or a replay is prepared. With several
    recorders it only reacts to the recorders that cover its object.
  - `RecordingTestMotion`, `RecordingTestActiveToggle`, `RecordingTestReparent`:
    transform, active-state and parent changes.
  - `RecordingTestSpawner`, `RecordingTestSpawnedObject`, `RecordingTestNetworkName`:
    create and remove (spawn and despawn) prefab instances with unique names.
  - `RecordingTestAudio`: plays a clip as one-shots or as a continuous loop.
  - `RecordingTestSpotLight`, `RecordingTestSpotLightRecorder`: animate a spot
    light's cone angle, color and intensity and record them with a custom
    `GenericRecorder` attached through an `IGenericRecorderProvider`.
  - `RecordingTestLightCone`: draws the spot light's cone as a translucent beam that
    follows the light's angles, color and intensity, live and during replay.
  - `RecordingTestRenderer`, `RecordingTestRendererRecorder`: animate and record a
    mesh renderer's color, emission and enabled state using a MaterialPropertyBlock.
  - `RecordingTestParticles`, `RecordingTestParticlesRecorder`: record a seeded,
    animated particle settings and reconstruct the fountain when seeking.
  - `RecordingTestRecorderStatus`: world-space status line of one recorder (state,
    file and time), used by the multi-recorder scene.
- `Editor/RecordingTestGenericRecorderChecks`: run **VRSYS > Recording > Validate
  Generic Recorder Samples** to check renderer state and particle seeking.
- `Shared`: the test sound (`Audio/RecordingTestChime.wav`) and the local spawned
  prefab (`Resources/VRSYS-Recording-Tests`) with its prefab mapping
  (`Prefab Information`).

The networked scene also needs VRSYS Core's **Sample Scene Setups** sample and
TextMesh Pro Essential Resources. Opening it offers to import the Core sample if
missing. In a new project, create a user role list through
**VRSYS > Core > Setup > User Role List** and add `HMD` and `Desktop` entries.
Read the README in each scene's folder for setup and usage instructions.

Both scenes present each test on a small column with a one-line TextMesh Pro
headline, so the scene itself explains what each object demonstrates. The shared
column prefab and its materials are under `Shared/Presentation`. Column labels
use TextMesh Pro Essential Resources' default LiberationSans SDF font.

The included native recording plugin supports Windows x86_64 and Android arm64.

## Starting and stopping the test objects

Select **Recording Test Objects** and toggle **Run Test Behaviours** on its
`RecordingTestObjectsController`. It is enabled by default. Disable it before
starting replay to keep procedural motion, activation, parent changes, spawning,
light animation and live test audio paused while you inspect recorded playback.
Re-enable it to resume the tests from their paused state. Objects already spawned
by the tests pause too, including networked instances spawned at the scene root.

This switch pauses test activity without disabling recording/replay components
or unregistering the generic recorder provider. Replay preparation still resets
the test objects and cleans up live spawned instances; the existing automatic
pause during replay also remains in effect. In a networked session, each peer
controls its own local tests; change the server's switch to pause the
server-authoritative networked tests.

## Development

In the VRSYS repository, edit this content under `Assets/RecordingSamplesWork`,
then copy its complete contents, including `.meta` files, to
`Packages/com.vrsys.recording/Samples~/Recording Tests` before distribution.
Test Package Manager import in a separate project so the working copy and imported
sample do not introduce duplicate script classes, GUIDs or Resources paths.

## Renderer and particle examples

Both scenes include **Renderer Appearance** and **Particle System** columns at
opposite ends of the second row. In the networked scene they belong to the
**Local** group: each peer animates and records its own copy, like the spotlight.

The floating cube smoothly changes from teal to coral, pulses its emission and
briefly hides once per eight seconds. Its GenericRecorder stores enabled state
in one integer and base/emission RGBA in eight floats. It changes only a
MaterialPropertyBlock, leaving the shared material untouched. Its gentle motion
uses the existing transform recorder.

The fountain swells between a narrow cyan stream and a wider lilac/pink spray.
The test script animates emission rate and enabled state, start speed, lifetime,
size, gravity, rotation, color, and cone angle/radius. The recorder captures these
scalar settings along with elapsed time, the seed, shape enabled state and
renderer visibility. Start color uses four Color32 bytes (8 bits per channel).

The v2 layout uses nine floats, five integers and four bytes of the existing
generic payload. It accepts earlier time/seed-only samples without overwriting
module settings. Replay restores the live settings afterward, including the
original curves/gradients and auto-random-seed setting.

For this example, both live animation and replay reconstruct an eight-second
local-space cycle using the settings at the sampled instant. This makes backward
seeks and paused playback independent of frame history; changing a property at
the same timestamp also updates the result. The cycle restarts with an empty
fountain, and each reconstruction simulates at most eight seconds. This is not a
general particle snapshot recorder: arbitrary parameter curves/gradients,
world-space motion, collisions, sub-emitters and external forces need additional
recorded state or simulation history.

The RecordingTestObjectsController also registers a root IGenericRecorderProvider
that attaches child custom recorders even when their test behaviours are disabled.
Providers re-register when scripts reload, including when a controller reference
is retained. The live-animation toggle does not disable recorder attachment.

Both examples register through IGenericRecorderProvider, stop their live animation
during replay and follow **Run Test Behaviours**. Renderer properties are restored
when replay ends. The sample includes small display and particle shaders that work
with Built-in and URP without changing the project's render pipeline.
