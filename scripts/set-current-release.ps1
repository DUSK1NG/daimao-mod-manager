param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$linkPath = Join-Path $projectRoot 'bin'
$targetPath = (Resolve-Path -LiteralPath $PackageDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $targetPath '呆猫mod manager.exe') -PathType Leaf)) {
    throw 'The release package must contain 呆猫mod manager.exe.'
}
if ($targetPath.Equals($linkPath, [StringComparison]::OrdinalIgnoreCase) -or
    $targetPath.StartsWith($linkPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The release package cannot be inside the bin entry point.'
}

$previousTarget = $null
$existing = Get-Item -LiteralPath $linkPath -Force -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    if ($existing.LinkType -ne 'Junction') {
        throw 'Root bin contains real files. Archive that directory before setting the current release.'
    }
    $previousTarget = $existing.Target
    if ($previousTarget.Equals($targetPath, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Output $linkPath
        return
    }
    # Delete only the junction itself; never recurse into a release package.
    [IO.Directory]::Delete($linkPath)
}

try {
    New-Item -ItemType Junction -Path $linkPath -Target $targetPath | Out-Null
}
catch {
    if ($previousTarget -and -not (Test-Path -LiteralPath $linkPath)) {
        New-Item -ItemType Junction -Path $linkPath -Target $previousTarget | Out-Null
    }
    throw
}
Write-Output $linkPath
