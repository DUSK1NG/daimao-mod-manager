param(
    [Parameter(Mandatory)][string]$Executable,
    [int]$Samples = 3,
    [Parameter(Mandatory)][string]$OutputFile
)

$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$statePath = Join-Path $env:LOCALAPPDATA 'HunterModManager\state.json'
$stateHash = if (Test-Path -LiteralPath $statePath) { (Get-FileHash -LiteralPath $statePath).Hash } else { $null }
$results = for ($sample = 1; $sample -le $Samples; $sample++) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $executablePath -WindowStyle Hidden -PassThru
    try {
        do {
            Start-Sleep -Milliseconds 50
            $process.Refresh()
            if ($process.HasExited) { throw 'Application exited before showing its window.' }
            if ($timer.Elapsed.TotalSeconds -gt 30) { throw 'Startup timed out.' }
        } while ($process.MainWindowHandle -eq [IntPtr]::Zero)
        $startupMs = $timer.ElapsedMilliseconds
        if (-not $process.WaitForInputIdle(10000)) { throw 'Window did not become idle.' }
        Start-Sleep -Seconds 3
        $process.Refresh()
        [pscustomobject]@{
            Sample = $sample
            StartupMs = $startupMs
            WorkingSetBytes = $process.WorkingSet64
            PrivateBytes = $process.PrivateMemorySize64
            CpuMs = $process.TotalProcessorTime.TotalMilliseconds
            Responding = $process.Responding
        }
    }
    finally {
        if (-not $process.HasExited) {
            if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(10000)) {
                $process.Kill()
                throw 'Application failed to close normally.'
            }
        }
        $process.Dispose()
    }
}
$finalHash = if (Test-Path -LiteralPath $statePath) { (Get-FileHash -LiteralPath $statePath).Hash } else { $null }
if ($stateHash -ne $finalHash) { throw 'The Mod state changed during measurement.' }
$report = [pscustomobject]@{
    Executable = $executablePath
    ExecutableBytes = (Get-Item -LiteralPath $executablePath).Length
    ExecutableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
    StateUnchanged = $true
    Samples = @($results)
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputFile) -Force | Out-Null
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputFile -Encoding utf8
$report | ConvertTo-Json -Depth 4
