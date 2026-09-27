# HANDOFF

## 目标

完成 Windows x64 便携式 Monster Hunter Mod Manager，管理 Steam 版 World、Rise、Wilds 的 ZIP/RAR/7Z 文件型 Mod 导入、预览和按勾选启停。PAK 首版只识别说明，不自动安装。设计范围见 `docs/superpowers/specs/2026-09-27-hunter-mod-manager-design.md`。

## 已实现

- WinForms 界面提供游戏目录选择、拖入/选择压缩包、目录锚点分析、手动映射、版本选择、文件预览、前置状态、Mod 勾选启停与操作记录。
- 窄窗体自动上下分栏；外层布局列随窗体伸缩，并在启动或 DPI 变化时限制窗体尺寸。
- Core 支持 ZIP/RAR/7Z 清单解析、锚点与前置配方映射；拒绝不安全路径、链接、加密及重复路径。混合 PAK/安装器的包会阻止部分安装。
- 导入默认停用；已有 Mod 文件冲突可在一次操作中切换。部署逐文件备份，并在启停时核对基线与当前哈希；跨进程锁保护状态和事务，原子替换目标文件。事务日志在恢复前核对目标范围和文件哈希，检测到外部改动时停止。
- 前置包通过明确配方选择文件；REFramework 通过官方 GitHub 下载入口。Nexus 前置由用户下载后导入本地压缩包。
- `scripts/build-release.ps1` 已生成 Win-x64 自包含发布包；包内含 7-Zip 26.03 的 `7z.exe`、`7z.dll`、`License.txt`、使用说明和哈希清单。

## 已验证

- 当前集成测试：21 项通过，0 项失败。测试使用临时模拟游戏目录，包含 ZIP/7Z 和通过环境变量提供的本机 RAR 样本；不写入真实游戏目录。
- 布局回归测试在当前 96 DPI 下通过：窄窗体可见右侧选择按钮、宽窗体左右分栏、窗体保持在屏幕可用区域。
- Core 与 App Release 构建成功，0 个错误。
- Stracker 前置配方已在模拟目录导入、启用、停用和恢复测试；此结果不等于对本机 World 安装目录写入验证。
- World 本机目录对三个 ZIP/RAR 包只读预览，Wilds 本机目录对临时脚本包只读预览。便携 ZIP 通过 7-Zip 完整性检查、包内清单与 ZIP 哈希核对；自包含 EXE 能启动且主窗口响应。

## 尚未验证

- 本机没有 Rise 安装；没有进行任何游戏内 Mod 加载验证。
- 没有向真实游戏目录写入或部署前置。
- 跨账户 UAC 提权流程和拖拽交互尚未人工实测。界面启动和控件树已检查；UI 内的预览输入未完成交互验证，核心预览由只读命令验证。
- 交付 ZIP 位于工作区 `outputs/` 中最新的 `HunterModManager-win-x64-*` 目录。

## 发布运行要求

- 开发/构建机：Windows x64、.NET 10 SDK、7-Zip 26.03 的 `7z.exe`、`7z.dll` 和官方 `License.txt`。
- 终端用户：解压即运行，不要求安装 .NET SDK；压缩组件随包提供。
- 推荐命令：`.\scripts\build-release.ps1 -Dotnet 'dotnet' -SevenZipDirectory 'C:\Program Files\7-Zip'`。

## 关键保护约束

- MHW 现有 `nativePC` 内容属于用户数据；按文件备份和恢复，不能清空该目录。
- 外部哈希变化时停止自动覆盖/删除，保留现场并提示用户处理。
- PAK 与独立安装器不得作为“部分成功”包部署；不得运行包内程序。
- REFramework 官方发布：https://github.com/praydog/REFramework-nightly/releases/
