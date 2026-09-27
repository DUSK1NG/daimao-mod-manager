@echo off
setlocal
cd /d "%~dp0"
if exist "src\HunterModManager.Wpf\bin\Release\net10.0-windows\HunterModManager.Wpf.exe" (
    start "" "src\HunterModManager.Wpf\bin\Release\net10.0-windows\HunterModManager.Wpf.exe"
    exit /b 0
)
if exist "src\HunterModManager.Wpf\bin\Debug\net10.0-windows\HunterModManager.Wpf.exe" (
    start "" "src\HunterModManager.Wpf\bin\Debug\net10.0-windows\HunterModManager.Wpf.exe"
    exit /b 0
)
echo 请先构建 HunterModManager.Wpf 项目。
exit /b 1
