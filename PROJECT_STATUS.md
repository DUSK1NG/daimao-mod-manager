# 项目状态

最后更新：2026-09-27

## 目标

交付 Windows x64 便携式工具，管理 Steam 版 Monster Hunter World、Rise、Wilds 的 ZIP/RAR/7Z 文件型 Mod。首版不自动安装 PAK。产品范围见 `docs/superpowers/specs/2026-09-27-hunter-mod-manager-design.md`。

## 当前实现

- 当前便携包使用重构后的 WPF 界面，保留旧 WinForms 实现。Core 导入/预览/启停流程保持不变。
- WPF 使用深色系统控件、纯色与矢量元素，无额外 UI 库、图片或常驻动画；窄窗缩小左栏，右侧表单滚动，主要操作始终可见。实施计划见 `docs/ui-refactor-plan-2026-09-27.md`。
- 支持游戏目录校验、归档路径分析、手动映射、版本选择、前置环境状态和明确前置包配方。
- 有文件哈希保护、逐文件基线备份/恢复、冲突切换、跨进程锁、原子替换和事务日志恢复。
- 构建脚本 `scripts/build-release.ps1` 发布 Windows x64 WPF 自包含 ZIP；原生 WPF 库打入单文件并在启动时解包。包内另含 7-Zip 组件、许可文本、README 和哈希清单。

## 已验证

- 集成测试：21 项通过、0 项失败。
- WPF 布局测试在 1100×760、800×760、700×720、640×540 下通过；实机截图检查了导入、前置与记录页。旧 WinForms 布局测试在当前 96 DPI 下通过。
- 整个解决方案 Release 构建：0 个错误。
- Stracker ZIP 前置配方在临时模拟目录通过导入、启用、停用和原文件恢复验证。
- 本机 World 目录对三个 ZIP/RAR 包只读预览；Wilds 目录对临时脚本包只读预览。实际目录无写入。
- WPF 便携 ZIP 包含 7 个文件，7-Zip 完整性检查通过；6 个内容文件和 ZIP 本身的 SHA-256 均与清单一致。自包含 EXE 启动后主窗口响应。一次空闲工作集观察约 113 MiB；这是本机单次测量，不代表跨设备上限。
- 测试未写入真实游戏目录。
- 当前 WPF ZIP 在新的临时目录解压后，内含 EXE 独立启动并出现响应的主窗口；构建脚本核对单文件发布未遗留必需原生库。打包测试时一次空闲工作集约 114 MiB，仅作本机参考。

## 待核验 / 边界

- 本机未安装 Rise；没有游戏内运行验证，不能确认 Mod 实际加载。
- 没有向真实游戏目录写入 Mod 或前置。
- 跨账户 UAC 提权和拖拽交互尚未人工实测；核心预览与部署逻辑由模拟目录和只读命令验证，不能声称游戏内有效。
- 当前 WPF 交付 ZIP 为 `artifacts/release/HunterModManager-win-x64-20260927-180205/HunterModManager-win-x64-20260927-180205.zip`，SHA-256 为 `ffd7c0042157f0e9f8ad05381348f1bd37fc3b23ec43ad9a0d3aa2127b80a2c0`；同目录有 `.zip.sha256`。旧版发布文件保留。
- 本次随包 7-Zip 为 26.03，包含 `7z.exe`、`7z.dll` 与官方 `License.txt`；构建脚本检查文件存在，但不验证版本号。

## 构建环境

仅开发/构建时需要 Windows x64 与 .NET 10 SDK。终端用户目标为自包含发布，无需安装 SDK。运行自动化集成测试：

```powershell
dotnet run --project tests/HunterModManager.Tests/HunterModManager.Tests.csproj
dotnet run --project tests/HunterModManager.WpfLayoutTests/HunterModManager.WpfLayoutTests.csproj
```

发布命令：

```powershell
.\scripts\build-release.ps1 -Dotnet 'dotnet' -SevenZipDirectory 'C:\Program Files\7-Zip'
```

## 保持的安全约束

- 不执行归档内 EXE/MSI/脚本；含 PAK 或安装器的混合包阻止部分导入。
- MHW 已存在的 `nativePC` 文件视为用户数据，不清空目录；覆盖前备份。
- 检出外部修改时停止自动覆盖或删除。
