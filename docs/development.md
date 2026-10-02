# 构建与测试

## 构建环境

在 Windows x64 准备 .NET 10 SDK、7-Zip 和 [NSIS 3.11](https://sourceforge.net/projects/nsis/files/NSIS%203/3.11/nsis-3.11.zip/download)。将 NSIS ZIP 解压到 `artifacts/tools/nsis`，使 `artifacts/tools/nsis/nsis-3.11/makensis.exe` 存在；7-Zip 默认从 `C:\Program Files\7-Zip` 读取，也可通过脚本的 `-SevenZipDirectory` 指定目录。

从仓库根目录执行：

```powershell
.\scripts\build-release.ps1
```

脚本输出当前程序入口、ZIP 和自解压包路径。运行产物位于 `artifacts/publish`，下载包位于 `artifacts/package`；根目录 `bin` 联接指向当前程序。保留包内许可与校验文件，详见[目录约定](directory-layout.md)。

一次实测的输出末段（省略仓库绝对路径）：

```text
bin\呆猫mod manager.exe
artifacts\package\DaiMaoModManager-win-x64-20261002-145446\DaiMaoModManager-win-x64-20261002-145446.zip
artifacts\package\DaiMaoModManager-win-x64-20261002-145446\DaiMaoModManager-Setup.exe
```

## 测试

核心集成测试使用临时游戏目录。运行前关闭对应游戏，否则游戏进程检查会拒绝启停模拟目录中的 Mod。

```powershell
dotnet run --project tests/HunterModManager.Tests/HunterModManager.Tests.csproj -c Release
dotnet run --project tests/HunterModManager.WpfLayoutTests/HunterModManager.WpfLayoutTests.csproj -c Release
```

一次实测的输出末行分别为：

```text
结果：21 项通过，0 项失败
工具缓存释放、复用和修复通过；画像解码、五种布局、界面启停与冲突预览通过。
```

RAR 或多版本 ZIP 样本可用 `HMM_RAR_FIXTURE`、`HMM_MULTIVARIANT_FIXTURE` 指定完整路径。以上测试结果不包含实际游戏内的 Mod 加载验收。
