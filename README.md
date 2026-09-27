# 呆猫mod manager

Windows x64 WPF 工具，用于预览、导入和管理 Steam 版《Monster Hunter: World》《Monster Hunter Rise》《Monster Hunter Wilds》的文件型 Mod。直接下载并运行 `呆猫mod manager.exe`；无需安装 .NET SDK 或 7-Zip。`daimao.svg` 是可编辑图标源文件，便携 ZIP 另附说明和校验清单。

## 使用方法

1. 关闭游戏，运行 `呆猫mod manager.exe`；若下载的是便携 ZIP，先解压再运行其中的同名 EXE。
2. 选择游戏，确认自动找到的游戏目录；如未找到，使用“更改目录”指向包含游戏 EXE 的目录。
3. 把 ZIP/RAR/7Z 拖入窗口，或点击“选择压缩包”。阅读安装预览；多版本包先选择一个版本。无明确目录的包可打开“高级选项：手动映射”，填写目标目录后重新分析。
4. 点击“导入所选版本”。导入后 Mod 默认停用；在左侧勾选以部署，取消勾选以撤回。文件冲突时阅读提示，可选择在同一操作中切换。
5. 如预览提示需要前置组件，打开“前置环境”标签。REFramework 可从官方 GitHub 下载并配置；Stracker's Loader 与 FirstNatives 需要自己下载作者的压缩包后在此导入。荒野 Loose File Loader 还需按界面提示完成游戏内设置。

Mod 包、原文件备份和状态存于 `%LOCALAPPDATA%\HunterModManager`。首次运行还会将内置的 7-Zip 组件与许可文本解压到该目录的 `tools` 子目录。遇到外部修改或恢复错误时，保留该目录和游戏文件，先检查提示，不要手工清空 `nativePC` 或 `natives`。

## 当前功能

- 拖入或选择 ZIP、RAR、7Z 后分析压缩包，展示识别版本、目标路径、前置提示和阻止原因；支持手动映射无锚点包，也要求多版本包明确选择版本。
- 导入默认停用。勾选启用、取消勾选停用；目标文件有其他已启用 Mod 时报告冲突，可选择在事务中切换。
- 窄窗口缩小左侧列表宽度，右侧表单可滚动且导入按钮保持可见；启动时将窗体保持在屏幕可用区域内。
- 支持明确识别的 `nativePC`、`natives`、REFramework 脚本/插件文件及受支持前置组件配方。无法安全确定安装内容时阻止导入。
- 检查绝对路径、路径穿越、大小写重名、链接、加密条目和解压体积；混有 PAK 或独立安装器的包会阻止部分安装，不执行包内程序。
- 启用前保留覆盖文件，停用时尝试恢复基线。发现游戏文件在工具之外被更改时停止覆盖或回滚。事务日志用于处理未完成操作。

## 游戏与前置范围

| 游戏 | 可管理内容 | 前置环境 |
| --- | --- | --- |
| World | 已识别的 `nativePC` 文件型 Mod | 检查 Stracker’s Loader；用户选择已下载的 ZIP/RAR/7Z，工具按配方选择文件并可在模拟目录验证导入与启停 |
| Rise | REFramework 脚本/插件，以及满足前置条件的 `natives` 散文件 | REFramework 从官方 GitHub 获取；FirstNatives 等 Nexus 包由用户下载后本地导入 |
| Wilds | REFramework 脚本/插件，以及满足 Loose File Loader 条件的 `natives` 散文件 | REFramework 从官方 GitHub 获取；Loose File Loader 需要在游戏内设置 |

Rise 和 Wilds 的 PAK 仅识别和说明，不自动编号、安装或卸载。工具报告文件已部署不等于已经验证游戏加载成功。REFramework 官方入口：[nightly releases](https://github.com/praydog/REFramework-nightly/releases/)；文件选择遵循[官方 README](https://github.com/praydog/REFramework/blob/master/README.md)。需要下载官方 REFramework 时，当前网络连接须能访问 GitHub。

## 数据保护

Mod 包、状态、事务记录和备份存放在 `%LOCALAPPDATA%\HunterModManager`。MHW 现有 `nativePC` 内容属于用户数据：工具按文件备份覆盖目标，不通过清空目录卸载 Mod。若原文件或已部署文件的哈希与记录不符，会停止自动处理并保留现场。

## 验证状态与限制

- Core、WinForms 与 WPF 的 Release 构建已完成，0 个错误；21 项核心集成测试通过。WPF 布局测试及实机检视覆盖 1100×760、800×760、700×720 和 640×540；前置和记录页已打开检查。旧 WinForms 布局测试也通过。
- 本机 World 目录对三个现有 ZIP/RAR 包完成只读预览，标出了覆盖目标；Wilds 目录用临时 REFramework 脚本包完成只读预览。测试和验证未向真实游戏目录写入文件。
- 本机没有 Rise 安装目录；未在任何游戏中启动验证 Mod 是否生效。
- WPF 便携 ZIP 通过 7-Zip 完整性与 SHA-256 校验；自包含 EXE 已启动并观察到主窗口响应。提权及游戏内加载未实测。

## 开发与发布

开发者需要 .NET 10 SDK 和 Windows x64 环境。运行集成测试：

```powershell
dotnet run --project tests/HunterModManager.Tests/HunterModManager.Tests.csproj
dotnet run --project tests/HunterModManager.WpfLayoutTests/HunterModManager.WpfLayoutTests.csproj
```

构建自包含的 Windows x64 EXE 和便携 ZIP：

```powershell
.\scripts\build-release.ps1 -Dotnet 'dotnet' -SevenZipDirectory 'C:\Program Files\7-Zip'
```

`-Dotnet` 可填写 .NET 10 SDK `dotnet.exe` 的绝对路径。脚本要求指定的 7-Zip 目录含有 `7z.exe`、`7z.dll` 和 `License.txt`，将它们嵌入 EXE，并输出单独可下载的 EXE、SVG、便携 ZIP 与 SHA-256 校验文件。RAR 样本测试可通过 `HMM_RAR_FIXTURE` 环境变量指定本地 RAR 文件；未提供时该项跳过。

## 第三方许可

EXE 内嵌 7-Zip 的 `7z.exe`、`7z.dll` 和官方 `License.txt`，运行时解压到本地缓存。`7z.dll` 主要代码按 GNU LGPL 发布，部分代码还受 unRAR 许可限制或 BSD 许可约束；详细义务和源码获取入口见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 与 [7-Zip 官方下载页](https://www.7-zip.org/download.html)。
