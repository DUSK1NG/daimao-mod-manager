# HANDOFF

## 当前交付

呆猫mod manager v0.1.2。入口 `bin/呆猫mod manager.exe` 指向 `artifacts/publish/DaiMaoModManager-win-x64-20261002-133336`。同标识的 `artifacts/package` 提供独立 EXE、便携 ZIP、自解压包和校验文件。

窗口默认为 960×720，目录栏改为单行，窄窗口提示文字自动换行，状态栏可查看完整提示。三款游戏、拖拽导入、版本选择、启停、冲突切换和前置环境入口均保留。

程序保留中英文运行资源。自解压包采用 NSIS 3.11、LZMA，普通权限解压到当前用户的程序目录；写入失败返回错误。Mod 状态与备份仍在 `%LOCALAPPDATA%\HunterModManager`。

## 发布

v0.1.2 发布页：<https://github.com/DUSK1NG/daimao-mod-manager/releases/tag/v0.1.2>。

## 维护

保持原文件备份、外部修改检测、跨进程锁与事务恢复。不清理用户 Mod 数据。目录说明见 `docs/directory-layout.md`；原始画像与界面缩略图在源码 Assets。bin 更新后才能归档其旧目标。
