[English](README.md) | [简体中文](README.zh-CN.md)

# Unity

Unity 6000.6.0f1 host for input, windows, editor integration and the Shared native renderers. Place it at `Unity/` beside Shared and Tools. Optional Mods have their own repositories.

Desktop targets are Windows x64, Linux x64 and macOS x64. On Apple Silicon, use an x64 Unity Editor/player and x64 dependencies through Rosetta to retain FMOD 1.10.14.

Windows supports D3D11, Vulkan and D3D12. Linux requires Unity's Vulkan graphics API and X11/XWayland windowing; macOS uses Unity Metal presentation with the shared Vulkan renderer through MoltenVK. The macOS MoltenVK library must support VK_EXT_metal_objects. Native plugins and dependencies are prepared for the selected runtime identifier; use the same architecture for Unity and all native libraries.

First follow the adjacent Tools README to restore game content, FMOD 1.10.14 runtime libraries and audio banks, build native plugins and synchronize managed assemblies. Install your licensed Odin Inspector in `Assets/Plugins/Sirenix/`, or supply it to the preparation tool.

Open this directory through Unity Hub, wait for package restore and script import, then run `Assets/Scenes/Game.unity`. The `Celeste > Launch` window also starts the game. Select Original or InstalledEverest through GameHost's Source Profile. Helper modules require `-IncludeHelpers` during preparation.

## Build a player

Install the target's Unity desktop build support module, then use `Celeste > Build Host Player` to build for the editor's operating system. Explicit entries are:

| Target    | Menu                            | executeMethod                                          |
| --------- | ------------------------------- | ------------------------------------------------------ |
| Linux x64 | Celeste > Build Linux 64 Player | Celeste.Unity.Editor.DesktopPlayerBuild.BuildLinuxMenu |
| macOS x64 | Celeste > Build macOS 64 Player | Celeste.Unity.Editor.DesktopPlayerBuild.BuildMacMenu   |

Run Linux builds in the Linux editor and macOS builds in the x64 macOS editor: native final packaging requires the matching host. On Apple Silicon, use the Intel editor and x64 dependencies through Rosetta.

For a batch build, replace the executable and project paths with your installed editor and checkout:

```sh
"/path/to/Unity" -batchmode -quit -projectPath "/path/to/Workspace/Unity" -executeMethod Celeste.Unity.Editor.DesktopPlayerBuild.BuildHostMenu
```

The macOS editor executable is `Unity.app/Contents/MacOS/Unity`. Linux output is `Builds/Unity/linux-x64/Celeste in Unity.x86_64`; macOS output is `Builds/Unity/osx-x64/Celeste in Unity.app`. Run `Start-Original.sh` or `Start-Everest.sh` beside the player to select the game profile.

## Build for the Web

Use Windows, Unity's Web Build Support module, the FMOD Engine 2.03.15
HTML5 SDK, and the Emscripten toolchain required by that SDK. After restoring
game content, run these commands from the workspace root, replacing the
dependency paths:

```powershell
pwsh ./Unity/Tools/Build-Effects-Web.ps1 `
  -UnityEditorExecutable '/path/to/Unity.exe'
pwsh ./Unity/Tools/Audio/Fmod/Html5/Build-ThreadedFmod.ps1 `
  -SdkPath '/path/to/fmod-html5' `
  -ToolchainPath '/path/to/emsdk' `
  -UnityEditorExecutable '/path/to/Unity.exe'
```

If your restored audio banks differ from the supplied page index, regenerate
it from your content:

```powershell
node ./Unity/Tools/Audio/Fmod/Html5/BuildBankPageIndex.cjs `
  ./Content/Celeste `
  ./Unity/Tools/Audio/Fmod/Html5/OriginalBankPages.json
```

In Unity, select the Web platform and the Celeste Web template, wait for
script import, then choose `Celeste > Build Web Player`. Output is written
to `Builds/Web/`. Serve it over HTTPS with
`Cross-Origin-Opener-Policy: same-origin` and
`Cross-Origin-Embedder-Policy: require-corp`; serve gzip build files with their
original MIME types and `Content-Encoding: gzip`.

Original contributions are licensed under MPL-2.0; see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).
