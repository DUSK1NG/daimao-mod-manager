# 目录约定

参考 [.NET SDK 的构建输出目录](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output)，按用途区分可运行程序、分发包和中间文件。这里借用 `publish`、`package` 名称组织便携程序，没有启用全局 `UseArtifactsOutput`，源码项目仍沿用 SDK 默认的 bin/obj 路径。

```text
src/                         程序源码，保留与命名空间对应的项目名
tests/                       自动测试
scripts/                     构建、打包和维护脚本
docs/                        使用与开发文档
bin/                         指向当前 publish 目录的联接，不是另一份程序
artifacts/
  publish/<build-id>/         可运行程序、说明与包内校验清单
  package/<build-id>/         独立 EXE、ZIP、自解压包、下载校验文件
  tools/7zip/                打包时嵌入的第三方组件及许可
  tools/nsis/nsis-3.11/       自解压包编译器及许可
  tmp/                       构建暂存、浏览器预览、试运行产物
  archive/                   旧发布包及历史开发文件
```

普通用途目录使用小写英文；多词名称使用连字符。项目名与 `Assets` 保持现有 .NET 源码命名，构建标识保留产品、平台和时间，例如 `DaiMaoModManager-win-x64-20260928-184856`。`tmp`、`tools`、`archive` 是本项目的补充约定，并非 .NET SDK 强制名称。

正式呆猫图像保存在 `src/HunterModManager.Wpf/Assets`。截图和设计试稿属于临时产物，完成检查后可以删除；发布包内的程序、许可和校验清单按完整包保留。

发布脚本先生成并打包新程序，再更新根目录 bin 联接。归档旧版前应确认 bin 已指向新目录。不要删除当前 publish 目录，它就是启动入口访问的实际文件。artifacts 不进入 Git；GitHub Release 附件单独上传。

Mod 缓存、启用状态和恢复备份属于用户数据，仍存于 `%LOCALAPPDATA%\HunterModManager`，不参与本项目的目录清理。
