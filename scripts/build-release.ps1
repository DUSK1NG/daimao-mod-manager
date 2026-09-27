param(
    [string]$Dotnet = 'dotnet',
    [string]$SevenZipDirectory = 'C:\Program Files\7-Zip',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts')
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$releaseName = 'HunterModManager-win-x64-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$workRoot = Join-Path $outputRoot $releaseName
$publishRoot = Join-Path $workRoot 'publish'
$packageRoot = Join-Path $workRoot 'HunterModManager'

foreach ($file in @('7z.exe', '7z.dll', 'License.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SevenZipDirectory $file) -PathType Leaf)) {
        throw "Missing 7-Zip component: $file"
    }
}
New-Item -ItemType Directory -Path $publishRoot, $packageRoot -Force | Out-Null
& $Dotnet publish (Join-Path $projectRoot 'src\HunterModManager.App\HunterModManager.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $publishRoot 'HunterModManager.App.exe') -Destination $packageRoot
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'tools') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $SevenZipDirectory '7z.exe'), (Join-Path $SevenZipDirectory '7z.dll'), (Join-Path $SevenZipDirectory 'License.txt') -Destination (Join-Path $packageRoot 'tools')
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
Write-Output $zipPath
