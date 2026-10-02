# HANDOFF

## 当前交付

呆猫mod manager v0.1.2。入口 `bin/呆猫mod manager.exe` 指向 `artifacts/publish/DaiMaoModManager-win-x64-20261002-133336`。同标识的 `artifacts/package` 提供独立 EXE、便携 ZIP、自解压包和校验文件。

窗口默认为 960×720，目录栏改为单行，窄窗口提示文字自动换行，状态栏可查看完整提示。三款游戏、拖拽导入、版本选择、启停、冲突切换和前置环境入口均保留。

程序保留中英文运行资源。自解压包采用 NSIS 3.11、LZMA，普通权限解压到当前用户的程序目录；写入失败返回错误。Mod 状态与备份仍在 `%LOCALAPPDATA%\HunterModManager`。

## 已验证

- 23 项核心集成测试通过，包含真实 RAR、多版本 ZIP、三款游戏前置、恢复、冲突与路径安全。
- WPF 工具缓存、画像、五种尺寸、界面启停和冲突预览通过。解决方案零警告、零错误。
- ZIP 完整性、中文文件名和清单通过。自解压、覆盖更新、用户文件保留、无效路径明确失败、解压程序启动通过。
- 运行采样前后真实 Mod 状态哈希相同；模拟测试没有修改真实游戏目录。没有完成游戏内加载、跨账户 UAC 和人工拖拽验收。
- EXE 135,123,547 字节，ZIP 55,833,122 字节，自解压包 42,775,064 字节。测量与边界见 `docs/performance.md`。

## 下一步

将已验证文件发布到公开 GitHub 仓库，并核对 Release 附件哈希：<https://github.com/DUSK1NG/daimao-mod-manager>。

## 维护

保持原文件备份、外部修改检测、跨进程锁与事务恢复。不清理用户 Mod 数据。目录说明见 `docs/directory-layout.md`；原始画像与界面缩略图在源码 Assets。bin 更新后才能归档其旧目标。

紧凑界面由 gpt-6.1-sol 子代理完成，根任务核对并验收。其余实现与验证使用本地工具，没有调用 Jev；工具耗时见执行记录，上游 usage 与实付费用 unknown。
