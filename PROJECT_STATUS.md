# 项目状态

最后更新：2026-09-28

## 当前交付

呆猫mod manager 为 Windows x64 自包含 WPF 程序，管理 Steam 版《世界》《崛起》《荒野》的文件型 Mod。可拖入 ZIP/RAR/7Z，预览目标路径，选择版本，导入后勾选启用、取消勾选停用。提供冲突切换、逐文件备份和恢复、前置环境配置。PAK 和独立安装器不自动部署。

- GitHub：<https://github.com/DUSK1NG/daimao-mod-manager>
- 最新下载：<https://github.com/DUSK1NG/daimao-mod-manager/releases/latest>
- 本地发布目录：`artifacts/release/DaiMaoModManager-win-x64-20260928-184856/`
- EXE：`呆猫mod manager.exe`
- EXE SHA-256：`55bc386c50210900999ad0c94232c7a4b5b93d834eba9f7d133a80767b74060f`
- ZIP SHA-256：`0dc0f215bf3b6f29593eaaf3c321381b803ce35485b3132ced89eb667aa4871d`

## 本次改动

- 版本列表直接提示“点选其中一个后才能导入”。多版本包仍需明确选择，单版本包自动选中。
- ZIP、7Z、RAR 测试覆盖导入、启用及停用。新增真实多版本 ZIP 的各版本启停检查；样本通过环境变量指定，不进入源码仓库或发行包。
- README 收短为下载、功能、使用方法和构建步骤。
- 发布目录只保留最新版。10 个旧发布目录可恢复地移入 `artifacts/previous-releases/`，未永久删除；批量删除被自动审批拒绝，只返回 `blocked by policy`。

## 已验证

- 22 项核心集成测试全部通过，包括三种压缩格式、多个安装版本、前置条件、冲突切换、原文件恢复、外部修改保护及中断恢复。
- WPF 布局测试通过 1100×760、800×760、700×720、640×540 四种尺寸。
- 整套 Release 构建：0 警告、0 错误。
- 新 EXE 启动后标题正确、主窗口有响应，并正常关闭。
- ZIP 通过 7-Zip 完整性检查，包内 5 个文件与 SHA-256 清单一致。
- 更新前后实际 Mod 状态文件哈希相同，4 个导入记录、2 个启用状态保留。

测试均使用临时模拟目录；真实游戏目录只做过只读预览。没有完成游戏内加载、跨账户 UAC、人工拖拽交互验证，本机也未安装 Rise。

## 维护

使用 `scripts/build-release.ps1` 生成自包含 EXE 和便携 ZIP。运行要求与命令见 README。下一次更新先验证新包，再处理旧发布文件；保留应用数据中的 Mod 包、状态、事务记录和备份。
