# Recording Tests

Import **Recording Tests** from **VRSYS Recording > Samples** in Package Manager.
One import includes both test scenes and the shared scripts they use:

- `Local Recording Test/Scenes/VRSYS-Recording-LocalTest.unity`: local recording
  and replay of transforms, active state and hierarchy changes.
- `Networked Recording Test/Scenes/VRSYS-Recording-NetworkedTest.unity`: networked
  recording and replay with recordable user prefabs.
- `Scripts`: `RecordingTestBehaviour`, `RecordingTestMotion`,
  `RecordingTestActiveToggle` and `RecordingTestReparent` in the
  `vrsys.recording.samples` assembly. These scripts are sample content and are only
  compiled into a consuming project when this sample is imported.

The networked scene also needs VRSYS Core's **Sample Scene Setups** sample and
TextMesh Pro Essential Resources. Opening it offers to import the Core sample if
missing. In a new project, create a user role list through
**VRSYS > Core > Setup > User Role List** and add `HMD` and `Desktop` entries.
Read the README in each scene's folder for setup and usage instructions.

The included native recording plugin supports Windows x86_64 and Android arm64.

## Development

In the VRSYS repository, edit this content under `Assets/RecordingSamplesWork`,
then copy its complete contents, including `.meta` files, to
`Packages/com.vrsys.recording/Samples~/Recording Tests` before distribution.
Test Package Manager import in a separate project so the working copy and imported
sample do not introduce duplicate script classes, GUIDs or Resources paths.
