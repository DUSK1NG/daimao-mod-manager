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
- 发布状态：本地验收完成，等待上传及服务端哈希核对。

## 功能与验证

默认窗口 960×720，目录栏单行，长提示换行，状态栏有完整文本提示。三款游戏、导入预览、版本选择、启停、冲突和前置配置功能保留。

23 项核心测试通过，包括真实 RAR、多版本 ZIP、三款游戏前置与恢复。WPF 缓存、画像、五种布局、界面启停和冲突预览通过；解决方案零警告、零错误。ZIP 完整性、中文文件名、自解压、更新、用户文件保留、无效目标路径失败与程序启动通过。采样期间真实 Mod 状态哈希不变。

尚未完成游戏内加载、跨账户 UAC 和人工拖拽验收。本机没有 Rise 安装目录，模拟目录测试不代表游戏内效果。

## 打包与维护

自解压包采用 NSIS 3.11，默认放置在 `%LOCALAPPDATA%\Programs\DaiMaoModManager`。便携 ZIP 可自行选择目录；独立 EXE 也可运行。终端用户无需安装 .NET 或 7-Zip。许可原文与 SHA-256 清单随包提供。

运行 `scripts/build-release.ps1` 生成三种交付并更新 bin 联接。artifacts 按 publish、package、tools、tmp、archive 分类，工具链包括 7-Zip 与 NSIS。用户 Mod 数据保存在 `%LOCALAPPDATA%\HunterModManager`。性能记录见 `docs/performance.md`。

本轮紧凑界面使用 gpt-6.1-sol 子代理；根任务整合、打包与验收。没有调用 Jev，工具耗时见执行记录，模型 usage 与实付费用 unknown。
