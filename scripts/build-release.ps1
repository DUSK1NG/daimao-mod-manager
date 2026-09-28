param(
    [string]$Dotnet = 'dotnet',
    [string]$SevenZipDirectory = 'C:\Program Files\7-Zip',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\release')
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$releaseName = 'DaiMaoModManager-win-x64-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$workRoot = Join-Path $outputRoot $releaseName
$publishRoot = Join-Path $workRoot 'publish'
$packageRoot = Join-Path $workRoot 'DaiMaoModManager'
$embeddedRoot = Join-Path $projectRoot 'artifacts\embedded-tools'
$exeName = '呆猫mod manager.exe'
& (Join-Path $projectRoot 'scripts\build-icon.ps1') | Out-Null

foreach ($file in @('7z.exe', '7z.dll', 'License.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SevenZipDirectory $file) -PathType Leaf)) {
        throw "Missing 7-Zip component: $file"
    }
}
New-Item -ItemType Directory -Path $publishRoot, $packageRoot, $embeddedRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $SevenZipDirectory '7z.exe'), (Join-Path $SevenZipDirectory '7z.dll'), (Join-Path $SevenZipDirectory 'License.txt') -Destination $embeddedRoot
& $Dotnet publish (Join-Path $projectRoot 'src\HunterModManager.Wpf\HunterModManager.Wpf.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:EmbedSevenZip=true -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
$extraFiles = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse | Where-Object {
    $_.Extension -ne '.pdb' -and $_.FullName -ne (Join-Path $publishRoot 'HunterModManager.Wpf.exe')
})
if ($extraFiles.Count -ne 0) { throw "Single-file publish left required files outside EXE: $($extraFiles.Name -join ', ')" }
Copy-Item -LiteralPath (Join-Path $publishRoot 'HunterModManager.Wpf.exe') -Destination (Join-Path $workRoot $exeName)
Copy-Item -LiteralPath (Join-Path $workRoot $exeName) -Destination (Join-Path $packageRoot $exeName)
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.svg') -Destination (Join-Path $workRoot 'daimao.svg')
Copy-Item -LiteralPath (Join-Path $workRoot 'daimao.svg') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.png') -Destination (Join-Path $workRoot 'daimao.png')
Copy-Item -LiteralPath (Join-Path $workRoot 'daimao.png') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $packageRoot

$manifest = foreach ($file in (Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($packageRoot, $file.FullName).Replace('\', '/')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
}
$manifest | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Encoding utf8
$zipPath = Join-Path $workRoot ($releaseName + '.zip')
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
('{0}  {1}' -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($zipPath)) |
    Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ascii
('{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $workRoot $exeName) -Algorithm SHA256).Hash.ToLowerInvariant(), $exeName) |
    Set-Content -LiteralPath (Join-Path $workRoot ($exeName + '.sha256')) -Encoding utf8

& (Join-Path $PSScriptRoot 'set-current-release.ps1') -PackageDirectory $packageRoot | Out-Null

Write-Output (Join-Path $projectRoot ('bin\' + $exeName))
Write-Output $zipPath
