# Networked Recording Test

Demonstrates networked recording and replay with test objects and recordable
desktop/HMD user prefabs.

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
   the **Replay** controls. Inspect synchronized transforms, active state and
   hierarchy changes.

## Included assets

- `../Scripts` contains the shared motion, active-state and reparenting test
  behaviours and their base class, compiled into `vrsys.recording.samples`.
- `Prefabs/VRSYS-Recording-NetworkedTest-NetworkPrefabsList.asset` registers the
  recordable user prefabs.
- `Prefabs/Prefab Information` contains the user prefab mappings for recording.
- `Resources/VRSYS-Recording-NetworkedTest` contains the recordable desktop and HMD
  prefabs. Preserve this folder structure so replay can load the prefabs in builds.

The prefab mapping assets retain the development asset paths. Replay uses the
portion after `Resources/`, so importing this sample under a different package
version does not require rewriting those paths.
