# HANDOFF

## 目标

完成 Windows x64 便携式 Monster Hunter Mod Manager，管理 Steam 版 World、Rise、Wilds 的 ZIP/RAR/7Z 文件型 Mod 导入、预览和按勾选启停。PAK 首版只识别说明，不自动安装。设计范围见 `docs/superpowers/specs/2026-09-27-hunter-mod-manager-design.md`。

## 已实现

- 当前便携包使用 WPF 界面，提供游戏目录选择、拖入/选择压缩包、手动映射、版本选择、文件预览、前置状态、Mod 勾选启停与操作记录；旧 WinForms 界面仍保留在源码中。
- WPF 使用系统控件和原有深色布局；主图 `Assets/daimao.png` 参照用户截图生成，紫蓝头套、米色圆脸、米色小脚，抱着 `MOD.zip` 文件夹。贴纸、蜡笔、像素三种透明画像和原创“老大”口吻短句出现在画像栏；640×540 时收起画像栏。小尺寸 ICO 帧使用 `daimao-compact.png`，`daimao.svg` 内嵌主图位图以保持相同外观，并非矢量路径。程序与 EXE 名称为“呆猫mod manager”。
- Core 支持 ZIP/RAR/7Z 清单解析、锚点与前置配方映射；拒绝不安全路径、链接、加密及重复路径。混合 PAK/安装器的包会阻止部分安装。
- 导入默认停用；已有 Mod 文件冲突可在一次操作中切换。部署逐文件备份，并在启停时核对基线与当前哈希；跨进程锁保护状态和事务，原子替换目标文件。事务日志在恢复前核对目标范围和文件哈希，检测到外部改动时停止。
- 前置包通过明确配方选择文件；REFramework 通过官方 GitHub 下载入口。Nexus 前置由用户下载后导入本地压缩包。
- `scripts/build-release.ps1` 发布 WPF Win-x64 自包含单个 EXE 和便携 ZIP。构建时重建图标，并将 7-Zip 的 `7z.exe`、`7z.dll`、`License.txt` 与第三方说明作为资源嵌入 EXE；运行时解压到 `%LOCALAPPDATA%\HunterModManager\tools\<哈希>\`。发布目录另有 PNG、SVG 和 EXE、ZIP 的 SHA-256 文件。

## 已验证

- 当前集成测试：20 项通过，0 项失败；RAR 样本缺失时跳过 1 项。测试使用临时模拟游戏目录，不写入真实游戏目录。
- WPF 布局测试在 1100×760、800×760、700×720、640×540 下通过；新版画像栏在前三种尺寸可见、640×540 时收起，1100×760 与 700×720 实机截图检查了文字换行和操作区。旧 WinForms 布局测试在当前 96 DPI 下也通过。
- 整个解决方案 Release 构建成功，0 个错误。
- Stracker 前置配方已在模拟目录导入、启用、停用和恢复测试；此结果不等于对本机 World 安装目录写入验证。
- World 本机目录对三个 ZIP/RAR 包只读预览，Wilds 本机目录对临时脚本包只读预览。新 EXE 从无外置 `tools` 的发布目录启动，主窗口标题为“呆猫mod manager”；释放的 7-Zip 二进制和许可文本与构建机原件 SHA-256 相同。ZIP 完整性检查和 EXE 哈希校验通过，系统提取的 32×32 程序图标可见。
- 构建脚本核对单文件发布未遗留必需原生库。旧版便携 ZIP 的临时目录启动与工作集数据仅属于旧版验证，不代表新版性能。

## 尚未验证

- 本机没有 Rise 安装；没有进行任何游戏内 Mod 加载验证。
- 没有向真实游戏目录写入或部署前置。
- 跨账户 UAC 提权流程和拖拽交互尚未人工实测。界面启动和控件树已检查；UI 内的预览输入未完成交互验证，核心预览由只读命令验证。
- 当前交付文件在 `artifacts/release/DaiMaoModManager-win-x64-20260927-185314/`：`呆猫mod manager.exe`、`daimao.png`、`daimao.svg`、同名便携 ZIP，以及 SHA-256 文件。旧版发布文件保留。

## 发布运行要求

- 开发/构建机：Windows x64、.NET 10 SDK、7-Zip 26.03 的 `7z.exe`、`7z.dll` 和官方 `License.txt`。
- 终端用户：直接运行单个 EXE，不要求安装 .NET SDK 或 7-Zip；首次运行释放内置压缩组件到本地应用数据目录。
- 推荐命令：`.\scripts\build-release.ps1 -Dotnet 'dotnet' -SevenZipDirectory 'C:\Program Files\7-Zip'`。

## 关键保护约束

- MHW 现有 `nativePC` 内容属于用户数据；按文件备份和恢复，不能清空该目录。
- 外部哈希变化时停止自动覆盖/删除，保留现场并提示用户处理。
- PAK 与独立安装器不得作为“部分成功”包部署；不得运行包内程序。
- REFramework 官方发布：https://github.com/praydog/REFramework-nightly/releases/
