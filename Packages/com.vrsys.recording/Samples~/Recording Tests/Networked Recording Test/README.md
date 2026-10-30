# Networked Recording Test

Demonstrates networked recording and replay with test objects and recordable
desktop/HMD user prefabs. The objects under **Recording Test Objects** are in two
groups:

- **Local**: the same tests as the Local Recording Test (transforms, active state,
  parent changes, `Spawned Cube <n>` instances that are created and destroyed, the
  one-shot, stationary looping and moving spatial audio sources, and the spot light recorded as generic
  data), plus the Renderer Appearance cube and seeded Particle System fountain.
  Every peer animates and records its own copy.
- **Networked**: NetworkObjects with server-authoritative NetworkTransforms that
  move, rotate, scale and change parent, plus a spawner that spawns a
  `Networked Spawned Sphere <n>` every 1.5 s and despawns it after 3.5 s, and a UFO
  (`Networked Teleporter (UFO)`) that teleports between two spots 1.1 m apart every
  2 s. Only the server drives them; Netcode syncs them to the clients.

Every test is displayed on its own small column with a fixed, single-line
world-space UI headline. The first two rows show the eleven local tests; the
third row has the six networked tests, labeled **Network Position**, **Network
Rotation**, **Network Scale**, **Network Parent**, **Network Spawn / Despawn** and
**Network Teleport**.

The UFO checks that teleports of networked objects stay teleports. While recording,
the server teleports it through its NetworkTransform. During replay with **Replay on
server only**, the server detects the jump (more than 1 m between two replay steps,
the same limit the recorder uses) and teleports it again, so clients see it jump
without sliding between the spots.
Labels stay visible when an object is deactivated, removed or moved to another
parent. The column and plaque assets are shared with the local scene.

Use **Run Test Behaviours** on the root's **Recording Test Objects Controller**
to pause the procedural tests before inspecting a replay. Each peer controls its
local tests; use the server's checkbox to pause the networked test animations and
spawner. Recording and replay components remain active while the tests are paused.

## Prerequisites

- Install VRSYS Recording and its VRSYS Core dependency through Package Manager.
- Import VRSYS Core's **Sample Scene Setups** sample. This scene references its
  network menu, keyboard, network prefab list and lobby settings. Opening this
  scene offers to import the Core sample if it is missing. The same import is
  available through **VRSYS > Recording > Import VRSYS Core "Sample Scene Setups"**.
- Import **Window > TextMeshPro > Import TMP Essential Resources** if the project's
  default TMP font resources are missing.
- In a new project, run **VRSYS > Core > Setup > User Role List**, then add `HMD`
  and `Desktop` to the list's **Role Entries** if missing. The network menu uses
  this project-wide list to select a user prefab. Use
  **VRSYS > Core > Setup > Default Layers** to configure interaction layers if needed.
- Use Windows x86_64 or Android arm64 for the included native recording plugin.
- Configure the project's XR runtime if using an HMD. For online lobby sessions,
  configure Unity Services as required by VRSYS Core's connection setup.

## Usage

1. Import **Recording Tests** from **VRSYS Recording > Samples** in Package Manager.
2. Open `Scenes/VRSYS-Recording-NetworkedTest.unity` and import the prerequisites above.
3. Enter Play mode and use the network menu to host or join a session. Use another
   player instance to test recording and replay across multiple users.
4. Select the scene's `RecorderController` component and click **Distributed** in
   the **Record** controls. Move the users and let the test objects animate, then
   click **Stop recording**.
5. Select the recording in **Selected Replay File** and click **Distributed** in
   the **Replay** controls. Check that transforms, active state and hierarchy
   changes, the spawned cubes and spheres (replay copies are named
   `...[Rec<id>]`), the sounds, spot light, renderer appearance and particle fountain
   repeat as recorded. Pause and scrub backward to check particle reconstruction.

Record at least eight seconds to capture a full orbit of **Audio Source (Moving
Spatial Loop)** in the Local group. Each peer records its local source's mono
loop and movement. On replay, check that the pulsing sphere follows the recorded
path and that the sound direction and distance change with its position.

Run the replay once with **Replay on server only** enabled (the server replays
networked objects and Netcode syncs them) and once with it disabled (every peer
replays them itself). **Spawn network prefabs** decides whether the server spawns
the replay copies of networked prefabs (such as the spawned spheres) for everyone,
or every peer creates a local copy.

## Included assets

- `../Scripts` contains the shared test behaviours, compiled into
  `vrsys.recording.samples`. See the README in the sample root for an overview.
- `../Shared` contains the test sound and the local spawned cube prefab.
- `Prefabs/VRSYS-Recording-NetworkedTest-NetworkPrefabsList.asset` registers the
  recordable user prefabs and the networked spawned sphere.
- `Prefabs/Prefab Information` contains the prefab mappings for recording.
- `Resources/VRSYS-Recording-NetworkedTest` contains the recordable desktop and HMD
  prefabs and `RecordingTestSpawnedSphere-Networked`. Preserve this folder
  structure so replay can load the prefabs in builds.

The prefab mapping assets retain the development asset paths. Replay uses the
portion after `Resources/`, so importing this sample under a different package
version does not require rewriting those paths.

`RecordingTestNetworkName` on the spawned sphere gives each instance the same name
on every peer (Netcode would otherwise name client instances `(Clone)`), because
recordings identify objects by their hierarchy path.
