# 项目状态

最后更新：2026-10-02

## 当前交付

呆猫mod manager v0.1.1，Windows x64 自包含 EXE 与便携 ZIP。

- 仓库：<https://github.com/DUSK1NG/daimao-mod-manager>
- 启动入口：`bin/呆猫mod manager.exe`
- 当前构建：`DaiMaoModManager-win-x64-20261002-123500`
- 程序目录：`artifacts/publish/<构建标识>/`
- 分发目录：`artifacts/package/<构建标识>/`
- EXE SHA-256：`764fa89a1e5d4338abf1f7ac23d53e36edccaebdd07179a05bda5fa7c2fdb577`
- ZIP SHA-256：`bb169bf603e5d00e649766bde09f57186c2f02d95f6b931ec4b6209bc796a2bf`
- 发布状态：v0.1.1 已公开并设为 Latest，三个下载附件的服务端 SHA-256 与本地一致。下载：<https://github.com/DUSK1NG/daimao-mod-manager/releases/tag/v0.1.1>。v0.1.0 附件保留。

## 优化结果

界面改用构建时生成的 204 像素画像，取消未使用的大图嵌入。内置组件改用流读写，启动时只检查一次缓存。预览与安装冲突查询使用路径索引；普通启停取消重复状态读取，并合并异常处理、去重和哈希读取。最近 200 条操作记录保留在界面，避免持续增长；及时释放游戏进程查询对象。

修复了旧包有独有文件时冲突切换失败的问题，并补充基线恢复测试。安全边界、备份和事务恢复继续保留。

本机重复启动的三次样本中位数：工作集 164.80 → 134.13 MiB，私有内存 136.42 → 87.75 MiB，累计 CPU 1750 → 1484 ms，窗口出现时间 889 → 887 ms。EXE 减少约 6.02 MiB，ZIP 减少约 5.94 MiB。首次启动新版另测得 6.62 秒，原因未定位；不宣称首次启动加速。完整记录见 `docs/performance.md`。

## 验证

23 项核心测试通过，覆盖 ZIP、7Z、真实 RAR、多版本包、备份恢复、冲突切换、外部修改、缓存丢失、多实例、路径安全和未完成事务恢复。WPF 测试覆盖工具缓存释放与修复、画像尺寸、四种窗口布局、界面启停状态同步和冲突预览。最终 ZIP 完整性与包内清单核对通过，运行测量前后真实 Mod 状态哈希相同。

没有进行游戏内加载、跨账户 UAC 和人工拖拽验收。本机没有 Rise 安装目录；模拟测试不等同于游戏内效果。

## 目录与维护

artifacts 按 publish、package、tools、tmp、archive 分类。旧稳定构建和中间构建已归档并核对旧 EXE 哈希；publish/package 各保留最终构建，bin 指向当前 publish。原始画像与生成的界面缩略图均保存在源码 Assets 中。

运行 `scripts/build-release.ps1` 打包并更新入口。性能复测使用 `scripts/measure-runtime.ps1`，报告放 `artifacts/tmp/performance`。Mod 数据仍保存在 `%LOCALAPPDATA%\HunterModManager`，不参与构建清理。早前被自动审批拒绝的预览 HTML 清理没有重试。

本轮没有调用 Jev 或新建子代理；工具耗时在执行记录中，模型 usage 与实付费用 unknown。

