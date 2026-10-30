# Multi-Recorder Test

Several recorders in one local scene, each working on its own part of the scene.
The scene uses the objects of the local test, split into two groups under
**Recording Test Objects**:

| Group | Objects | Front/back |
|---|---|---|
| **Group A** | position, rotation, scale and combined transform changes, parent change, activation | front row |
| **Group B** | renderer appearance, one-shot, continuous and moving audio, spawn/despawn, spot light (generic data), particles | back row |

Three recorders (`RecordingSetup` instances) are configured with
**Attach to all transforms** off and a list of **Recording roots**
(`RecorderController.recordingRoots`):

| Recorder | Recorder ID | Recording roots | Recording name |
|---|---|---|---|
| **Recorder A (Group A)** | 1 | Group A | `MultiRecorderA` |
| **Recorder B (Group B)** | 2 | Group B | `MultiRecorderB` |
| **Recorder C (Group A + B)** | 3 | Group A, Group B | `MultiRecorderAB` |

A recorder only records the objects under its roots (transforms, audio sources
and the generic data of the test objects there), and replays them in place: a
replay without a replay root only drives objects beneath the recorder's roots.
The status lines above the rows show each recorder's state, file and time.

All recordings are stored in the project `VRSYS-Recording-MultiRecorder`
(a subfolder of the recording folder). Only Recorder A reacts to the keyboard and
controller shortcuts; use the Inspector buttons of each recorder for the tests.
Microphone and audio listener recording are off. The scene is local only:
recorder IDs are fixed per recorder and are not synchronized over the network.

## What to test

Select a recorder's `RecorderController` in Play mode and use its **Record** and
**Replay** controls (**Local**). **Run Test Behaviours** on
**Recording Test Objects** starts and stops the live test animations.

1. **Simultaneous recordings.** Start a recording on A and on B, let the tests
   run for at least ten seconds, stop both. A's recording contains only Group A,
   B's only Group B (objects spawned in Group B during the recording included).
2. **Two replays at once.** Disable **Run Test Behaviours**, replay
   `MultiRecorderA` on A and `MultiRecorderB` on B, started a moment apart. Both
   groups move as recorded, each with its own replay time. Pausing, scrubbing or
   stopping one replay does not affect the other. Spawned cubes are recreated as
   `Spawned Cube <n>[Rec2]` under the spawner.
3. **Replay next to live objects.** Enable **Run Test Behaviours** and replay
   only on A. Group A follows the recording, Group B keeps its live animation
   and audio.
4. **Recording of a running replay.** While A replays Group A (tests on), start
   a recording on C and stop it after about ten seconds, then stop A.
   `MultiRecorderAB` now contains the replayed Group A and the live Group B.
5. **Replay of the combined recording.** Disable **Run Test Behaviours** and
   replay `MultiRecorderAB` on C: Group A repeats the replayed motion (shifted by
   the time A had already played when C started recording), Group B the live
   motion, including spawned cubes, light and audio.
6. **The same objects claimed twice.** While C replays both groups, start a
   replay on A. A leaves Group A to C (the console reports how many recorded
   objects were left to another recorder) and does not create copies; the
   objects keep following C. Stopping A does not affect C.
7. **Next generation.** While C replays `MultiRecorderAB`, record on A and B
   with new recording names (for example `MultiRecorderA_Gen2` and
   `MultiRecorderB_Gen2`), then replay those two in parallel and compare them
   with the original.

## How it works

- `RecorderController.recordingRoots` limits recording, the audio sources that
  are recorded, objects spawned during a recording and an in-place replay to the
  roots' subtrees. With **Attach to all transforms** on, the list is ignored.
- `RecorderController.CoversObject` tells whether a recorder records or replays
  an object in its current state (its replay root during a replay, otherwise its
  recording scope). The sample's test behaviours use it: they pause only while a
  recorder replays their own object, reset only for a recorder that covers them,
  and the generic data providers attach one recorder component per recorder, so
  one recorder can replay the data while another records it.
- During replay preparation, an object that another recorder is already
  replaying stays with that recorder. An object another recorder is only
  recording can be replayed, which is what makes recording a running replay
  possible.
