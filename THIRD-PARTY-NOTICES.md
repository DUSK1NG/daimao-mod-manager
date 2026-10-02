# 第三方组件与许可说明

## 7-Zip 26.03

发布脚本 `scripts/build-release.ps1` 从 `-SevenZipDirectory` 指定目录取得 `7z.exe`、`7z.dll` 和随 7-Zip 发布的 `License.txt`，嵌入单文件 EXE。程序运行时把它们和本说明解压到 `%LOCALAPPDATA%\HunterModManager\tools\<版本哈希>\`。未来重建时应确认所指目录的版本；脚本检查文件存在，但不会自行验证版本号。

7-Zip 官方许可文件说明：`7z.dll` 的大部分代码采用 GNU LGPL，少部分代码采用带 unRAR 限制的 LGPL，还有部分代码采用 BSD 3-Clause 或 BSD 2-Clause；其他 7-Zip 文件采用 GNU LGPL。二进制再分发须附上官方 `License.txt` 所要求的许可信息。应随发行包保留并分发从对应 7-Zip 版本取得的原版许可文件。

unRAR 限制规定：unRAR 源码不得用于重新实现专有的 RAR 压缩算法；单独或作为其他软件一部分分发修改版 unRAR 源码时，文档和源码注释须明确说明该代码不能用于开发兼容 RAR（WinRAR）的压缩程序。项目使用 7-Zip 进行归档列举和解压，不提供 RAR 压缩功能。具体适用条款以随包的原版许可文本为准。

源码获取：[7-Zip 官方下载页](https://www.7-zip.org/download.html)（页面提供 7-Zip 源码下载）。许可原文：[7-Zip License.txt](https://www.7-zip.org/license.txt)。发布时应随 `License.txt` 一并提供适用于 LGPL 的源码或源码获取信息，并保留适用的 BSD 声明。

本项目不会把 7-Zip 代码静态链接进应用；归档功能通过从 EXE 资源解压的 `7z.exe` 进程和 `7z.dll` 实现。此说明是发行记录，不替代针对实际二进制、源码版本及发行方式的许可核查。

## NSIS 3.11

自解压便携包由 NSIS 3.11 生成，使用 LZMA 压缩。NSIS 主体采用 zlib/libpng 许可；所用 LZMA 模块采用 Common Public License 1.0。原版许可随包提供为 `LICENSE-NSIS.txt`。

构建工具取自 [NSIS 官方发行文件](https://sourceforge.net/projects/nsis/files/NSIS%203/3.11/nsis-3.11.zip/download)，对应源码可从 [NSIS 官方源码仓库 v311](https://github.com/nsis-dev/nsis/tree/v311) 获得。项目没有修改 NSIS 或压缩模块。
