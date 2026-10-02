# 呆猫mod manager
Windows 文件型 Mod 管理工具，用于预览、导入、启用和停用《怪物猎人：世界》《崛起》《荒野》的 Mod。

<img src="src/HunterModManager.Wpf/Assets/daimao.svg" alt="抱着 MOD.zip 的呆猫" width="96">

## 快速开始

从[发布页](https://github.com/DUSK1NG/daimao-mod-manager/releases/latest)下载 `DaiMaoModManager-Setup.exe` 并双击解压，或完整解压 ZIP 后运行 `呆猫mod manager.exe`。运行包面向 Windows x64，已包含所需运行库和解压组件。

## 使用

关闭游戏，选择游戏目录，拖入 ZIP、RAR 或 7Z，查看目标路径并选择一个安装版本，再导入。勾选 Mod 启用，取消勾选停用并恢复被覆盖的文件；存在路径冲突时按提示切换 Mod。

导入只保存包和记录。支持的文件布局、前置组件、手动映射和拒绝安装的情况见[使用说明](docs/usage.md)。游戏文件被其他工具改动时，启停会停止并保留备份。

## 配置

程序自动查找游戏目录，也可在窗口顶部更改。压缩包、启用状态与备份保存在 `%LOCALAPPDATA%\HunterModManager`；前置组件在“前置环境”页配置。

## 开发

构建和测试命令见[开发说明](docs/development.md)，输出位置见[目录约定](docs/directory-layout.md)。测试使用临时游戏目录；实际游戏内加载需另行验证。

## 许可证

分发包中 7-Zip 与 NSIS 的许可、源码获取方式见[第三方组件说明](THIRD-PARTY-NOTICES.md)。本工具与 Capcom 无隶属关系。
