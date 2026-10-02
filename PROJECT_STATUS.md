# 项目状态

最后更新：2026-10-02

## 当前交付

呆猫mod manager 管理 Steam 版《世界》《崛起》《荒野》的标准文件型 Mod，提供压缩包预览、版本选择、导入、勾选启停、冲突切换、文件备份恢复及前置配置。

- 仓库：<https://github.com/DUSK1NG/daimao-mod-manager>
- 公开下载：<https://github.com/DUSK1NG/daimao-mod-manager/releases/latest>，现有 v0.1.0 附件保持原样。
- 固定启动入口：`bin/呆猫mod manager.exe`，通过目录联接访问当前 publish 目录。
- 当前程序：`artifacts/publish/DaiMaoModManager-win-x64-20260928-191731/`
- 当前分发包：`artifacts/package/DaiMaoModManager-win-x64-20260928-191731/`
- 当前 EXE SHA-256：`199c1aad9994ea1eb5a01bc7bba751384c520563a04ca76c140eb9d5ae3b13d1`
- 当前 ZIP SHA-256：`1226f8d6b1470bf4e2e1fc421b2be5d3b2a6fcfbc921746e1848f05632d16a01`

本地包用于验证整理后的构建流程，与已公开的 v0.1.0 附件分别记录。

## 目录整理

参考 .NET SDK 的输出类型约定，artifacts 下只保留五类目录：`publish` 放可运行程序，`package` 放分发文件，`tools` 放构建组件，`tmp` 放暂存与试运行文件，`archive` 放历史文件。详细说明和命名规则见 `docs/directory-layout.md`。源码项目继续使用默认 bin/obj。

- 已删除 27 张旧截图、图标试稿和图标预览。正式源码素材及完整发布包中的图标保留。
- 原 release、previous-releases、embedded-tools 已分别迁移到 package、archive/releases、tools/7zip。预览浏览器配置和试运行输出归入 tmp。
- 构建脚本与 WPF 嵌入资源路径已同步更新；新包生成后自动更新根目录 bin。
- 上一份本地构建已归档；publish 与 package 中各保留一份当前构建。
- 残留预览 HTML 与空目录的清理命令被自动审批拒绝，仅返回 `blocked by policy`，因此仍保留在 `artifacts/tmp/icon-preview`。

## 验证

本次重新运行了完整发布脚本。新路径下的自包含 EXE、ZIP 和校验文件生成成功；ZIP 完整性与包内 5 个文件的 SHA-256 验证通过。bin 与实际 EXE 的文件 ID 相同；从 bin 启动的窗口标题正确、正常响应并正常退出。验证前后实际 Mod 状态文件哈希相同。

2026-09-28 的功能基线为 22 项核心集成测试全部通过，以及四种尺寸的 WPF 布局测试通过。本次只调整构建和目录组织，没有重复执行这些未受影响的功能测试。

尚未完成游戏内加载、跨账户 UAC、人工拖拽交互验证；本机没有 Rise 安装。模拟目录测试与实际游戏效果分别记录。

## 维护

运行 `scripts/build-release.ps1` 打包，再由 `scripts/set-current-release.ps1` 更新启动入口。归档旧版前确认 bin 已指向新程序；保留当前 publish 目录。Mod 包、状态、事务日志和恢复备份仍存于 `%LOCALAPPDATA%\HunterModManager`，不参与构建目录清理。
