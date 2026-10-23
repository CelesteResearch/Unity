[English](README.md) | [简体中文](README.zh-CN.md)

# Unity

Unity 6000.6.0f1 工程，提供输入、窗口、编辑器与共享原生渲染器接入。放在工作区 `Unity/`，依赖相邻 Shared、Tools；可选 Mod 各自使用独立仓库。

桌面目标为 Windows x64、Linux x64 和 macOS x64。Apple Silicon 通过 Rosetta 使用 x64 Unity 编辑器／Player 及原生依赖，以保留 FMOD 1.10.14。

Windows 支持 D3D11、Vulkan 和 D3D12；Linux 需要 Unity 的 Vulkan 图形 API 与 X11／XWayland 窗口系统；macOS 使用 Unity Metal 呈现，并通过 MoltenVK 执行共享 Vulkan 渲染器。macOS 的 MoltenVK 需要支持 VK_EXT_metal_objects。原生插件和依赖按选定运行时标识准备，Unity 与所有原生库应使用相同架构。

先在父级工作区按 `Tools/README.md` 恢复原版资源、FMOD 1.10.14 运行库和音频 banks，构建原生插件并同步程序集。Unity 另需使用者安装已授权 Odin Inspector。

通过 Unity Hub 打开本目录，等待包恢复与导入，然后打开 `Assets/Scenes/Game.unity` 进入 Play；也可使用 `Celeste > Launch`。GameHost 的 Source Profile 选择 Original 或 InstalledEverest。Helper 模块需在准备入口显式添加 `-IncludeHelpers`。

## 构建 Player

安装目标平台的 Unity 桌面构建支持模块，再使用 `Celeste > Build Host Player` 按编辑器所在系统构建。也可显式选择：

| 目标      | 菜单                            | executeMethod                                          |
| --------- | ------------------------------- | ------------------------------------------------------ |
| Linux x64 | Celeste > Build Linux 64 Player | Celeste.Unity.Editor.DesktopPlayerBuild.BuildLinuxMenu |
| macOS x64 | Celeste > Build macOS 64 Player | Celeste.Unity.Editor.DesktopPlayerBuild.BuildMacMenu   |

Linux 构建使用 Linux 编辑器，macOS 构建使用 x64 macOS 编辑器；原生包的最终处理需要匹配的系统。Apple Silicon 通过 Rosetta 使用 Intel 编辑器与 x64 依赖。

批处理构建时，将可执行文件和工程路径替换为自己的编辑器与源码位置：

```sh
"/path/to/Unity" -batchmode -quit -projectPath "/path/to/Workspace/Unity" -executeMethod Celeste.Unity.Editor.DesktopPlayerBuild.BuildHostMenu
```

macOS 的编辑器可执行文件位于 `Unity.app/Contents/MacOS/Unity`。Linux 产物位于 `Builds/Unity/linux-x64/Celeste in Unity.x86_64`，macOS 产物位于 `Builds/Unity/osx-x64/Celeste in Unity.app`。使用产物旁的 `Start-Original.sh` 或 `Start-Everest.sh` 选择游戏配置。

## 构建 Web 版本

使用 Windows、Unity 的 Web Build Support 模块、FMOD Engine 2.03.15 HTML5 SDK，
以及该 SDK 要求的 Emscripten 工具链。恢复游戏素材后，在工作区根目录执行以下命令，
并替换依赖路径：

```powershell
pwsh ./Unity/Tools/Build-Effects-Web.ps1 `
  -UnityEditorExecutable '/path/to/Unity.exe'
pwsh ./Unity/Tools/Audio/Fmod/Html5/Build-ThreadedFmod.ps1 `
  -SdkPath '/path/to/fmod-html5' `
  -ToolchainPath '/path/to/emsdk' `
  -UnityEditorExecutable '/path/to/Unity.exe'
```

如果恢复的音频 bank 与现有分页索引不同，用自己的素材重新生成索引：

```powershell
node ./Unity/Tools/Audio/Fmod/Html5/BuildBankPageIndex.cjs `
  ./Content/Celeste `
  ./Unity/Tools/Audio/Fmod/Html5/OriginalBankPages.json
```

在 Unity 中选择 Web 平台和 Celeste Web 模板，等待脚本导入完成后，
选择 `Celeste > Build Web Player`。产物位于 `Builds/Web/`。
通过 HTTPS 提供服务，并设置 `Cross-Origin-Opener-Policy: same-origin` 和
`Cross-Origin-Embedder-Policy: require-corp`；gzip 构建文件需要设置其原始 MIME 类型及
`Content-Encoding: gzip`。

自有贡献采用 MPL-2.0，见 [LICENSE](LICENSE) 和 [NOTICE.md](NOTICE.md)。
