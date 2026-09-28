# HANDOFF

## 目标与当前结果

交付“呆猫mod manager”，管理 Steam 版《世界》《崛起》《荒野》的标准文件型 Mod。当前 WPF 程序提供 ZIP/RAR/7Z 导入、安装预览、版本选择、勾选启停、冲突切换及前置环境配置。完整设计见 `docs/superpowers/specs/2026-09-27-hunter-mod-manager-design.md`。

最新构建为 `artifacts/release/DaiMaoModManager-win-x64-20260928-184856/`，提供独立 EXE、便携 ZIP、图标和校验文件。10 个旧发布目录已移到 `artifacts/previous-releases/`；永久删除命令被自动审批拒绝，因此采用可恢复归档。

GitHub 仓库：<https://github.com/DUSK1NG/daimao-mod-manager>。下载入口：<https://github.com/DUSK1NG/daimao-mod-manager/releases/latest>。发布后以远程 Release 的实际资产列表为准。

## 本次验证

- 22 项核心集成测试全部通过，覆盖 ZIP/7Z/RAR 的导入和启停、真实多版本 ZIP 的分别部署、冲突与恢复、外部修改和危险路径保护。
- RAR 样本由 `HMM_RAR_FIXTURE` 指定，多版本 ZIP 由 `HMM_MULTIVARIANT_FIXTURE` 指定。样本不随源码或发行包上传。
- WPF 四种尺寸布局测试通过；整套 Release 构建 0 警告、0 错误。
- 新 EXE 启动并正常退出；ZIP 完整性与包内文件 SHA-256 验证通过。
- 实际应用状态文件在更新前后未变。测试没有向真实游戏目录部署文件。

仍未验证游戏内加载、跨账户 UAC 以及人工拖拽交互。本机没有 Rise 安装，不能宣称三款游戏内效果已验证。

## 保持的约束

- 现有游戏文件按文件备份和恢复，不清空 `nativePC` 或 `natives`。
- 检出外部文件修改时停止自动覆盖或删除，保留现场。
- 多版本包必须明确选定版本。PAK、独立安装器不得作为部分成功包部署，不执行包内程序。
- REFramework 仅从官方 GitHub 配置；Nexus 前置包由用户自行下载后导入。
- Mod 状态和备份保存在 `%LOCALAPPDATA%\HunterModManager`，更新程序不得删除此目录。
- EXE 内嵌 7-Zip 组件和原版许可，首次运行释放到应用数据目录的工具缓存。发布脚本要求构建机具备 .NET 10 SDK 和 7-Zip，终端用户无需另装它们。

后续验收及准确哈希见 `PROJECT_STATUS.md`。当前仅根代理执行定位、修改、测试和打包，未调用 Jev 或新建子代理；工具输出包含耗时，上游模型 usage 和实付费用不可得，记为 unknown。
