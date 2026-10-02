# 项目状态

最后更新：2026-10-02

## 当前交付

呆猫mod manager v0.1.2，Windows x64。

- 仓库：<https://github.com/DUSK1NG/daimao-mod-manager>
- 入口：`bin/呆猫mod manager.exe`
- 构建：`DaiMaoModManager-win-x64-20261002-133336`
- 程序目录：`artifacts/publish/<构建标识>`；分发目录：`artifacts/package/<构建标识>`。
- 独立 EXE：135,123,547 字节；SHA-256 `84577ee14ed297f474c87894cfe9dcb494466c70898899f33a4f45f456bae325`。
- ZIP：55,833,122 字节；SHA-256 `f6a08cd73e784808a4245d659c6669785fec2129c8c29e179c9c99fc4e0bc6db`。
- 自解压包：42,775,064 字节；SHA-256 `79bd14234c96fe04cbd5ddc282de0648a96ab61a6e7f1c266b5eb514654b4f95`。
- 下载：<https://github.com/DUSK1NG/daimao-mod-manager/releases/tag/v0.1.2>。

## 功能

默认窗口 960×720，目录栏单行，长提示换行，状态栏有完整文本提示。三款游戏、导入预览、版本选择、启停、冲突和前置配置功能保留。

## 打包与维护

自解压包采用 NSIS 3.11，默认放置在 `%LOCALAPPDATA%\Programs\DaiMaoModManager`。便携 ZIP 可自行选择目录；独立 EXE 也可运行。终端用户无需安装 .NET 或 7-Zip。许可原文与 SHA-256 清单随包提供。

运行 `scripts/build-release.ps1` 生成三种交付并更新 bin 联接。artifacts 按 publish、package、tools、tmp、archive 分类，工具链包括 7-Zip 与 NSIS。用户 Mod 数据保存在 `%LOCALAPPDATA%\HunterModManager`。性能记录见 `docs/performance.md`。
