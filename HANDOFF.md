# HANDOFF

## 当前结果

已交付“呆猫mod manager”，支持 Steam 版《世界》《崛起》《荒野》的标准文件型 Mod。设计见 `docs/superpowers/specs/2026-09-27-hunter-mod-manager-design.md`，目录约定见 `docs/directory-layout.md`。

当前启动入口为 `bin/呆猫mod manager.exe`，联接目标为 `artifacts/publish/DaiMaoModManager-win-x64-20260928-191731/`。分发文件位于同一构建标识对应的 `artifacts/package/` 子目录。公开 v0.1.0 附件未改写；本地重新打包验证新的目录结构。

GitHub：<https://github.com/DUSK1NG/daimao-mod-manager>；公开下载：<https://github.com/DUSK1NG/daimao-mod-manager/releases/latest>。

## 本次改动与验证

- 删除了 27 张旧截图及图标试稿。正式素材保存在 `src/HunterModManager.Wpf/Assets`，发布包内图标按完整包保留。
- artifacts 现按 publish、package、tools、tmp、archive 分类；原目录已迁移，构建脚本和 WPF 嵌入组件路径已更新。
- 根目录 bin 自动指向当前程序，上一份本地构建已归档。
- 完整发布脚本运行成功。ZIP 完整性与包内 5 个文件哈希通过，bin 与实际 EXE 文件 ID 一致，程序启动和退出正常，实际 Mod 状态未变。
- 清理残留预览 HTML 与空目录的命令被自动审批拒绝，仅返回 `blocked by policy`；它们保留在 tmp/icon-preview。

2026-09-28 的功能基线为 22 项核心测试和四种尺寸 WPF 布局测试通过。本次构建目录改动未触及 Mod 安装逻辑，未重复运行功能基线。准确哈希见 PROJECT_STATUS.md。

没有完成游戏内加载、跨账户 UAC 和人工拖拽交互验证。本机没有 Rise 安装。

## 维护约束

- 游戏原有文件按文件备份和恢复，不清空 nativePC 或 natives；外部修改时停止自动覆盖或删除。
- 多版本包明确选择版本；PAK 和独立安装器不自动部署，不执行包内程序。
- REFramework 从官方 GitHub 配置；Nexus 前置包由用户下载后导入。
- Mod 数据仍保存在 `%LOCALAPPDATA%\HunterModManager`，构建或更新不得清理此目录。
- 更新 bin 后再归档旧 publish；bin 是目录联接，删除当前实际程序会使入口失效。
- 7-Zip 组件构建暂存于 tools/7zip，运行时仍释放至应用数据工具缓存；发布须保留许可和第三方说明。

本次定位、目录迁移和验证由根任务使用确定性工具完成，未调用 Jev 或新建子代理。工具往返耗时见执行记录，上游模型 usage 与实付费用为 unknown。
