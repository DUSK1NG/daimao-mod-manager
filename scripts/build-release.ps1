param(
    [string]$Dotnet = 'dotnet',
    [string]$SevenZipDirectory = 'C:\Program Files\7-Zip',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\package')
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$releaseName = 'DaiMaoModManager-win-x64-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$workRoot = Join-Path $outputRoot $releaseName
$publishRoot = Join-Path $projectRoot ('artifacts\tmp\publish\' + $releaseName)
$packageRoot = Join-Path $projectRoot ('artifacts\publish\' + $releaseName)
$embeddedRoot = Join-Path $projectRoot 'artifacts\tools\7zip'
$exeName = '呆猫mod manager.exe'
$nsisDirectory = Join-Path $projectRoot 'artifacts\tools\nsis\nsis-3.11'
$nsisCompiler = Join-Path $nsisDirectory 'makensis.exe'
if (-not (Test-Path -LiteralPath $nsisCompiler -PathType Leaf)) { throw 'NSIS 3.11 is required in artifacts/tools/nsis/nsis-3.11.' }
& (Join-Path $projectRoot 'scripts\build-icon.ps1') | Out-Null

foreach ($file in @('7z.exe', '7z.dll', 'License.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SevenZipDirectory $file) -PathType Leaf)) {
        throw "Missing 7-Zip component: $file"
    }
}
New-Item -ItemType Directory -Path $workRoot, $publishRoot, $packageRoot, $embeddedRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $SevenZipDirectory '7z.exe'), (Join-Path $SevenZipDirectory '7z.dll'), (Join-Path $SevenZipDirectory 'License.txt') -Destination $embeddedRoot
& $Dotnet publish (Join-Path $projectRoot 'src\HunterModManager.Wpf\HunterModManager.Wpf.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:EmbedSevenZip=true -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
$extraFiles = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse | Where-Object {
    $_.Extension -ne '.pdb' -and $_.FullName -ne (Join-Path $publishRoot 'HunterModManager.Wpf.exe')
})
if ($extraFiles.Count -ne 0) { throw "Single-file publish left required files outside EXE: $($extraFiles.Name -join ', ')" }
Copy-Item -LiteralPath (Join-Path $publishRoot 'HunterModManager.Wpf.exe') -Destination (Join-Path $workRoot $exeName)
Copy-Item -LiteralPath (Join-Path $workRoot $exeName) -Destination (Join-Path $packageRoot $exeName)
Copy-Item -LiteralPath (Join-Path $SevenZipDirectory 'License.txt') -Destination (Join-Path $packageRoot 'LICENSE-7ZIP.txt')
Copy-Item -LiteralPath (Join-Path $nsisDirectory 'COPYING') -Destination (Join-Path $packageRoot 'LICENSE-NSIS.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $packageRoot

$manifest = foreach ($file in (Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($packageRoot, $file.FullName).Replace('\', '/')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
}
$manifest | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Encoding utf8
$zipPath = Join-Path $workRoot ($releaseName + '.zip')
& (Join-Path $SevenZipDirectory '7z.exe') a -tzip -mm=Deflate -mx=7 -mcu=on $zipPath (Join-Path $packageRoot '*')
if ($LASTEXITCODE -ne 0) { throw "ZIP creation failed: $LASTEXITCODE" }
('{0}  {1}' -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($zipPath)) |
    Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ascii
('{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $workRoot $exeName) -Algorithm SHA256).Hash.ToLowerInvariant(), $exeName) |
    Set-Content -LiteralPath (Join-Path $workRoot ($exeName + '.sha256')) -Encoding utf8

$setupPath = Join-Path $workRoot 'DaiMaoModManager-Setup.exe'
$setupScript = Join-Path ([IO.Path]::GetTempPath()) ('daimao-setup-' + [Guid]::NewGuid().ToString('N') + '.nsi')
$nsis = @'
Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"
Name "呆猫mod manager"
OutFile "@SETUP@"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
InstallDir "$LOCALAPPDATA\Programs\DaiMaoModManager"
!define MUI_ICON "@ICON@"
!define MUI_PAGE_HEADER_TEXT "解压呆猫mod manager"
!define MUI_PAGE_HEADER_SUBTEXT "程序将放在当前用户的程序目录。"
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "准备好了，老大！"
!define MUI_FINISHPAGE_TEXT "程序已解压到：$\r$\n$INSTDIR"
!define MUI_FINISHPAGE_RUN "$INSTDIR\呆猫mod manager.exe"
!define MUI_FINISHPAGE_RUN_TEXT "打开呆猫mod manager"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "SimpChinese"
Function .onInit
  System::Call 'kernel32::GetCommandLineW() w .r0'
  ${GetOptionsS} $0 "/D=" $1
  IfErrors done
  StrCpy $INSTDIR $1
done:
FunctionEnd
Section
  ClearErrors
  SetOutPath "$INSTDIR"
  IfErrors failed
  File "@PACKAGE@\呆猫mod manager.exe"
  IfErrors failed
  File "@PACKAGE@\README.md"
  IfErrors failed
  File "@PACKAGE@\THIRD-PARTY-NOTICES.md"
  IfErrors failed
  File "@PACKAGE@\LICENSE-7ZIP.txt"
  IfErrors failed
  File "@PACKAGE@\LICENSE-NSIS.txt"
  IfErrors failed
  File "@PACKAGE@\SHA256SUMS.txt"
  IfErrors failed
  Goto done
failed:
  SetErrorLevel 2
  Abort "无法写入程序目录，请检查路径和权限，并关闭正在运行的程序。"
done:
SectionEnd
'@
try {
    $nsis.Replace('@SETUP@', $setupPath).Replace('@PACKAGE@', $packageRoot).Replace('@ICON@', (Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.ico')) |
        Set-Content -LiteralPath $setupScript -Encoding utf8BOM
    & $nsisCompiler /V2 $setupScript
    if ($LASTEXITCODE -ne 0) { throw "Self-extracting package creation failed: $LASTEXITCODE" }
}
finally { Remove-Item -LiteralPath $setupScript }
('{0}  {1}' -f (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($setupPath)) |
    Set-Content -LiteralPath ($setupPath + '.sha256') -Encoding ascii

& (Join-Path $PSScriptRoot 'set-current-release.ps1') -PackageDirectory $packageRoot | Out-Null

Write-Output (Join-Path $projectRoot ('bin\' + $exeName))
Write-Output $zipPath
Write-Output $setupPath
