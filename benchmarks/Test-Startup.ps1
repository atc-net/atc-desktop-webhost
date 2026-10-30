<#
.SYNOPSIS
    Starts the sample once in bench mode and checks that the page reported its first paint.

.DESCRIPTION
    A smoke test for CI: it catches a window, webview or bridge that no longer works, which unit tests can't.
    The app exits by itself after first paint; this script fails when it doesn't within the timeout, exits with
    an error, or writes no PAGE_firstPaint line. Windows and Linux (on Linux, run it under xvfb-run on a server).

.EXAMPLE
    pwsh benchmarks/Test-Startup.ps1
    xvfb-run -a pwsh benchmarks/Test-Startup.ps1 -TimeoutSeconds 120
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string] $Fixture = (Join-Path $PSScriptRoot 'fixtures/text-1kb.txt'),
    [int] $TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
if (-not $Exe) {
    $name = if ($IsWindows) { 'atc-webhost-sample.exe' } else { 'atc-webhost-sample' }
    $Exe = Join-Path (Split-Path $PSScriptRoot) "sample/Atc.Desktop.WebHost.Sample/bin/Release/net10.0/$name"
}

if (-not (Test-Path $Exe)) { throw "Not found: $Exe (build the solution in Release first)" }
if (-not (Test-Path $Fixture)) { throw "Not found: $Fixture" }

$report = Join-Path ([IO.Path]::GetTempPath()) "atc-webhost-smoke-$PID.txt"
Remove-Item $report -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $Exe -ArgumentList @('--bench', '--bench-out', "`"$report`"", "`"$Fixture`"") -PassThru

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill($true)
    throw "The app didn't exit within $TimeoutSeconds s: the page never reported first paint."
}

if ($process.ExitCode -ne 0) { throw "The app exited with code $($process.ExitCode)." }
if (-not (Test-Path $report)) { throw "The app wrote no bench report." }

$lines = Get-Content $report
$lines | Write-Host
foreach ($key in 'STARTUP_MS', 'PAGE_firstPaint') {
    if (-not ($lines | Where-Object { $_ -like "$key=*" })) { throw "The bench report has no $key line." }
}

Write-Host "Startup smoke test passed." -ForegroundColor Green