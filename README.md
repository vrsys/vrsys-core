# VRSYS Core

VRSYS Core offers a framework that allows to quick setup a Unity3D project for XR developments.
The framework is build on Unity version 2021.3.26f1 and should also allow to be used with later versions.

## Discord
If you need help with using the package, you want to participate in further developments of the framework or just want to stay up to date with the latest releases, join our official VRSYS Core Discord server: https://discord.gg/sZ3YxqZZEJ

## Dependencies
To use the package in your project, first include the following packages:

### If you use VRSYS Core only:
- Authentication v2.4.0
- Input System v1.7.0
- Lobby v1.1.2
- Netcode for GameObjects v1.8.1
- Relay v1.0.5
- XR Interaction Toolkit v2.5.3
- XR Plugin Management v4.4.0
- [ART DTRACK Plugin v1.1.3](https://github.com/ar-tracking/UnityDTrackPlugin/releases/tag/v1.1.3)

### If you want to use the 4Players Odin Voice integration:
- [Odin SDK v1.6.4](https://github.com/4Players/odin-sdk-unity/releases/tag/v1.6.4)

## Documentation

### Setup
- Import the package into the target Unity project ("Assets/Import Package/Custom Package")
- Include required external packages ("Window/Package Manager")
- Under "XR Plugin-Management" select OpenXR as Plugin-In Provider for Windows
- Under "XR Plugin-Management/OpenXR" add the corresponding Interaction Profile that should be used (e.g. Meta Quest Touch Pro Controller Profile)
- Import TMP Essential ("Window/TextMeshPro/Import TMP Essential Resources")

### Single Scene Test
- Start the scene "Assets/VRSYS/Core/Demo/Demo Scenes/Single-Scene Setup/VRSYS-SingleScene"
- In the dropdown menu select the target device (e.g. HMD). For Quest controllers selection is done using the "A" button.
- Add or join a lobby
- You should now be in a networked version of the scene and see other users that have joined your lobby.
 
t.b.a.
