<#
.SYNOPSIS
    End-to-end tests: runs the sample's self-test in a real window, and on Windows the DevTools checks.

.DESCRIPTION
    1. Self-test (Windows and Linux): starts the sample with --selftest. The page checks the host's requests, events,
       files, mounts and frames through the real webview and bridge, the app writes a report and exits. Fails when
       the app hangs, exits with an error, writes no report, a check fails, or fewer than -MinPassed checks pass.
    2. DevTools checks (Windows): a real file drop and the user's close with the close guard on, driven over
       WebView2's DevTools protocol by devtools-checks.mjs. Needs Node 22 or later; skip with -SkipDevTools.

    On Linux, run it under xvfb-run on a server. Build the solution in Release first.

.EXAMPLE
    pwsh test/EndToEnd/Test-EndToEnd.ps1
    xvfb-run -a pwsh test/EndToEnd/Test-EndToEnd.ps1
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string] $Fixture = (Join-Path $PSScriptRoot '../../benchmarks/fixtures/text-1kb.txt'),
    [int] $TimeoutSeconds = 120,
    [int] $MinPassed = 30,
    [switch] $SkipDevTools
)

$ErrorActionPreference = 'Stop'
if (-not $Exe) {
    $name = if ($IsWindows) { 'atc-webhost-sample.exe' } else { 'atc-webhost-sample' }
    $Exe = Join-Path $PSScriptRoot "../../sample/Atc.Desktop.WebHost.Sample/bin/Release/net10.0/$name"
}

if (-not (Test-Path $Exe)) { throw "Not found: $Exe (build the solution in Release first)" }
$Exe = (Resolve-Path $Exe).Path
$Fixture = (Resolve-Path $Fixture).Path

function Stop-App([System.Diagnostics.Process] $process) {
    if (-not $process.HasExited) { $process.Kill($true) }
}

# --- 1. Self-test ---------------------------------------------------------------------------------------------------

$report = Join-Path ([IO.Path]::GetTempPath()) "atc-webhost-selftest-$PID.txt"
Remove-Item $report -ErrorAction SilentlyContinue
$app = Start-Process -FilePath $Exe -ArgumentList @('--selftest', "--selftest-out=`"$report`"", "`"$Fixture`"") -PassThru
if (-not $app.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-App $app
    throw "The self-test didn't finish within $TimeoutSeconds s."
}

if (-not (Test-Path $report)) { throw "The self-test wrote no report (exit code $($app.ExitCode))." }
$lines = Get-Content $report
$lines | Write-Host
$passed = [int](($lines | Where-Object { $_ -like 'SELFTEST_PASSED=*' }) -replace '.*=', '')
if ($lines -notcontains 'SELFTEST=pass') { throw 'The self-test failed (see the report above).' }
if ($app.ExitCode -ne 0) { throw "The self-test passed but the app exited with code $($app.ExitCode)." }
if ($passed -lt $MinPassed) { throw "Only $passed checks passed; expected at least $MinPassed." }
Write-Host "Self-test passed: $passed checks." -ForegroundColor Green

# --- 2. DevTools checks (Windows) -----------------------------------------------------------------------------------

if (-not $IsWindows -or $SkipDevTools) { return }

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) { throw 'The DevTools checks need Node 22 or later (or pass -SkipDevTools).' }
$major = [int]((& node --version) -replace '^v(\d+).*', '$1')
if ($major -lt 22) { throw "The DevTools checks need Node 22 or later; found $(& node --version)." }

# The dropped file lives in a folder the app hasn't been given, so the drop is what grants it.
$dropFolder = Join-Path ([IO.Path]::GetTempPath()) "atc-webhost-drop-$PID"
New-Item -ItemType Directory -Force $dropFolder | Out-Null
$dropFile = Join-Path $dropFolder 'dropped.txt'
Set-Content -Path $dropFile -Value 'dropped on the window'

$port = 9300 + ($PID % 500)
$app = Start-Process -FilePath $Exe -ArgumentList @('--isolated', "--browser-args=--remote-debugging-port=$port") -PassThru
try {
    & node (Join-Path $PSScriptRoot 'devtools-checks.mjs') $port $app.Id $dropFile
    if ($LASTEXITCODE -ne 0) { throw 'The DevTools checks failed.' }
    Write-Host 'DevTools checks passed.' -ForegroundColor Green
}
finally {
    Stop-App $app
    Remove-Item $dropFolder -Recurse -Force -ErrorAction SilentlyContinue
}