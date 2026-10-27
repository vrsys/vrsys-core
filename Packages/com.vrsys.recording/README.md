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

## Samples

Open **Window > Package Manager**, select **VRSYS Recording**, and open **Samples**.
Click **Import** beside the sample you want to use. Unity copies it into
`Assets/Samples/VRSYS Recording/<package version>/<sample name>`.

- **Recording Tests**: imports both scenes and their shared testing scripts in one step.
  - Open `Local Recording Test/Scenes/VRSYS-Recording-LocalTest.unity` for animated
    transforms, active state changes and hierarchy changes without a network session.
  - Open `Networked Recording Test/Scenes/VRSYS-Recording-NetworkedTest.unity` for
    networked test objects and recordable desktop/HMD user prefabs.
  The four testing scripts live in the sample's `Scripts` folder and compile into
  `vrsys.recording.samples` only when imported. They are not part of the package's
  runtime assembly.
- **Recording Sample**: the original recording sample scene.

The networked sample also requires VRSYS Core's **Sample Scene Setups** sample.
Opening the networked scene offers to import it if it is missing. It can also be
imported through VRSYS Core's Package Manager entry or
**VRSYS > Recording > Import VRSYS Core "Sample Scene Setups"**.
In a new project, run **VRSYS > Core > Setup > User Role List** and add `HMD` and
`Desktop` entries if they are missing. The network menu uses this project-wide
list. Configure VRSYS Core's default layers for interaction as needed.
Import **Window > TextMeshPro > Import TMP Essential Resources** if the project
does not already contain the default TMP font resources. See each sample's README
for usage and prerequisites. The included native recording plugin supports Windows
x86_64 and Android arm64.

### Updating the samples

The packaged copies are under `Samples~` and are ignored by Unity until imported.
For this repository, edit the scenes and shared scripts in `Assets/RecordingSamplesWork`,
then copy the complete contents and their `.meta` files into `Samples~/Recording Tests`
before distributing the package. Preserve the sample READMEs and their metadata.
Keep the `Resources/VRSYS-Recording-NetworkedTest` folder structure: replay loads
those prefabs by their path within `Resources`.

Use a separate project to test Package Manager import. Importing the packaged
copies into this development project while `Assets/RecordingSamplesWork` is present
would introduce duplicate asset GUIDs and duplicate Resources paths.

## Documentation

The framework documentation can be found here: https://vrsys.gitbook.io/vrsys (still under construction)
