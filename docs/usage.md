# Mod 导入与启停

## 运行包

`DaiMaoModManager-Setup.exe` 将程序解压到 `%LOCALAPPDATA%\Programs\DaiMaoModManager`，完成后可打开程序。ZIP 可完整解压到自选目录；独立 EXE 也可直接运行。程序目录与 Mod 缓存、状态和备份目录相互独立。

## 操作顺序

1. 关闭游戏，打开程序，选择对应游戏。确认顶部目录包含该游戏的 EXE；未找到时使用“更改目录”。
2. 拖入压缩包或使用“选择压缩包”，查看安装版本与文件预览。多版本包必须点选其中一个版本。
3. 点击“导入所选版本”。此时保存包和安装记录，尚未写入游戏目录。
4. 在左侧勾选 Mod 启用，取消勾选停用。工具记录覆盖前的文件内容，并在停用时恢复。
5. 两个 Mod 写入同一路径时，按提示停用冲突 Mod 后切换；工具外的文件改动会使启停停止，保留现场供复核。

## 支持的布局

| 游戏 | 文件布局及前提 |
| --- | --- |
| 世界 | 目录明确的 `nativePC` 文件 |
| 崛起 | REFramework 脚本、插件；配置散文件加载前置组件后的 `natives` 文件 |
| 荒野 | REFramework 脚本、插件；满足 Loose File Loader 条件后的 `natives` 文件 |

无法确定目标目录的普通文件包，可在“高级选项：手动映射”指定目录后重新分析。PAK 与独立安装器会提示原因，不自动安装；压缩包内的程序不会被执行。

## 前置组件

预览提示缺少组件时打开“前置环境”页。World 的 Stracker's Loader 与 Rise 的 FirstNatives 需下载后选取本地安装包；Rise、Wilds 的 REFramework 可从[官方发布页](https://github.com/praydog/REFramework-nightly/releases/)配置。Wilds 散文件加载还需检查界面提示的游戏设置。

管理器对文件导入和恢复的检查不等于游戏已正确加载该 Mod；前置组件与 Mod 是否适配游戏版本应在游戏内确认。
