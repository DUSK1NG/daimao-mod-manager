# 项目状态

最后更新：2026-09-27

## 目标

交付 Windows x64 便携式工具，管理 Steam 版 Monster Hunter World、Rise、Wilds 的 ZIP/RAR/7Z 文件型 Mod。首版不自动安装 PAK。产品范围见 `docs/superpowers/specs/2026-09-27-hunter-mod-manager-design.md`。

## 当前实现

- WinForms 界面和 Core 导入/预览/启停流程已实现。
- 界面布局已改为随窗口宽度切换分栏，并修复高缩放时固定宽度造成的右侧裁切。
- 支持游戏目录校验、归档路径分析、手动映射、版本选择、前置环境状态和明确前置包配方。
- 有文件哈希保护、逐文件基线备份/恢复、冲突切换、跨进程锁、原子替换和事务日志恢复。
- 构建脚本 `scripts/build-release.ps1` 已生成 Windows x64 自包含 ZIP；包内包含 7-Zip 组件、许可文本、README 和哈希清单。

## 已验证

- 集成测试：21 项通过、0 项失败。
- 布局回归测试在当前 96 DPI 下通过，覆盖窄窗、宽窗与启动尺寸限制。
- Core Release 构建与 App Release 构建：0 个错误。
- Stracker ZIP 前置配方在临时模拟目录通过导入、启用、停用和原文件恢复验证。
- 本机 World 目录对三个 ZIP/RAR 包只读预览；Wilds 目录对临时脚本包只读预览。实际目录无写入。
- 便携 ZIP 包含 7 个文件，7-Zip 完整性检查通过；6 个内容文件和 ZIP 本身的 SHA-256 均与清单一致。自包含 EXE 启动后主窗口响应，控件树可读取。
- 测试未写入真实游戏目录。

## 待核验 / 边界

- 本机未安装 Rise；没有游戏内运行验证，不能确认 Mod 实际加载。
- 没有向真实游戏目录写入 Mod 或前置。
- 跨账户 UAC 提权和拖拽交互尚未人工实测；核心预览与部署逻辑由模拟目录和只读命令验证，不能声称游戏内有效。
- 交付包位于工作区 `outputs/` 中最新的 `HunterModManager-win-x64-*` 目录；外部 ZIP 哈希文件为同名 `.zip.sha256`。
- 本次随包 7-Zip 为 26.03，包含 `7z.exe`、`7z.dll` 与官方 `License.txt`；构建脚本检查文件存在，但不验证版本号。

## 构建环境

仅开发/构建时需要 Windows x64 与 .NET 10 SDK。终端用户目标为自包含发布，无需安装 SDK。运行自动化集成测试：

```powershell
dotnet run --project tests/HunterModManager.Tests/HunterModManager.Tests.csproj
```

发布命令：

```powershell
.\scripts\build-release.ps1 -Dotnet 'dotnet' -SevenZipDirectory 'C:\Program Files\7-Zip'
```

## 保持的安全约束

- 不执行归档内 EXE/MSI/脚本；含 PAK 或安装器的混合包阻止部分导入。
- MHW 已存在的 `nativePC` 文件视为用户数据，不清空目录；覆盖前备份。
- 检出外部修改时停止自动覆盖或删除。
