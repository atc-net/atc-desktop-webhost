<#
.SYNOPSIS
    Builds Atc.Desktop.WebHost's sample and the comparison hosts in benchmarks/hosts, then measures them back to back
    in one session and prints a markdown table.

.DESCRIPTION
    Every host opens the same files: by default text-1kb.txt (startup with almost nothing to show) and
    text-10mb.txt (startup with a large document), both with random line lengths. Missing fixtures are created
    with New-Fixtures.ps1.

    WebView2 start time drifts within and between sessions, so the hosts are measured in rounds with the order
    reversed every other round (A B C, C B A, ...). Each table combines all rounds. Windows only.

.EXAMPLE
    pwsh benchmarks/Compare-Hosts.ps1
    pwsh benchmarks/Compare-Hosts.ps1 -Rounds 4 -Runs 10 -Hosts webhost,wpf -Fixtures text-1kb.txt,readme-5k.md
#>
[CmdletBinding()]
param(
    [string[]] $Fixtures = @('text-1kb.txt', 'text-10mb.txt'),
    [int] $Rounds = 2,
    [int] $Runs = 10,
    [string[]] $Hosts = @('webhost', 'wpf', 'winui'),
    [switch] $NoBuild,
    [string] $OutDir = (Join-Path $PSScriptRoot 'results/compare')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$Hosts = @($Hosts | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$Fixtures = @($Fixtures | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$fixtureDir = Join-Path $PSScriptRoot 'fixtures'
if ($Fixtures | Where-Object { -not (Test-Path (Join-Path $fixtureDir $_)) }) {
    & (Join-Path $PSScriptRoot 'New-Fixtures.ps1')
}

$catalog = [ordered]@{
    webhost = @{
        Name    = 'Atc.Desktop.WebHost'
        Project = 'sample/Atc.Desktop.WebHost.Sample/Atc.Desktop.WebHost.Sample.csproj'
        Exe     = 'sample/Atc.Desktop.WebHost.Sample/bin/Release/net10.0/atc-webhost-sample.exe'
    }
    wpf     = @{
        Name    = 'WPF + WebView2'
        Project = 'benchmarks/hosts/Wpf/AtcBench.Wpf.csproj'
        Exe     = 'benchmarks/hosts/Wpf/bin/Release/net10.0-windows/atc-bench-wpf.exe'
    }
    winui   = @{
        Name    = 'WinUI 3 + WebView2'
        Project = 'benchmarks/hosts/WinUI/AtcBench.WinUI.csproj'
        Exe     = 'benchmarks/hosts/WinUI/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/atc-bench-winui.exe'
    }
}

foreach ($key in $Hosts) {
    if (-not $catalog.Contains($key)) { throw "Unknown host '$key'. Known: $($catalog.Keys -join ', ')" }
}

if (-not $NoBuild) {
    foreach ($key in $Hosts) {
        Write-Host "Building $($catalog[$key].Name)..."
        dotnet build (Join-Path $root $catalog[$key].Project) -c Release --nologo -v quiet | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $key" }
    }
}

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force $OutDir | Out-Null

for ($round = 1; $round -le $Rounds; $round++) {
    $order = @($Hosts)
    if ($round % 2 -eq 0) { [array]::Reverse($order) }
    Write-Host ""
    Write-Host "Round $round of ${Rounds}: $($order -join ' -> ')" -ForegroundColor Cyan
    foreach ($fixture in $Fixtures) {
        foreach ($key in $order) {
            & (Join-Path $PSScriptRoot 'measure.ps1') `
                -Exe (Join-Path $root $catalog[$key].Exe) `
                -Fixture (Join-Path $fixtureDir $fixture) `
                -Label "$key-$([IO.Path]::GetFileNameWithoutExtension($fixture))-r$round" `
                -Runs $Runs `
                -OutDir $OutDir
        }
    }
}

function Get-Median([double[]] $values) {
    if (-not $values -or $values.Count -eq 0) { return $null }
    $sorted = $values | Sort-Object
    return [math]::Round($sorted[[math]::Max(0, [math]::Ceiling(0.5 * $sorted.Count) - 1)], 0)
}

function Get-P90([double[]] $values) {
    $sorted = $values | Sort-Object
    return [math]::Round($sorted[[math]::Max(0, [math]::Ceiling(0.9 * $sorted.Count) - 1)], 0)
}

$lines = @()
foreach ($fixture in $Fixtures) {
    $fixtureName = [IO.Path]::GetFileNameWithoutExtension($fixture)
    $rows = foreach ($key in $Hosts) {
        $summaries = 1..$Rounds | ForEach-Object { Get-Content (Join-Path $OutDir "$key-$fixtureName-r$_.json") -Raw | ConvertFrom-Json }
        $allRuns = @($summaries | ForEach-Object { $_.runValues })
        $column = { param($name) Get-Median @($allRuns | Where-Object { $null -ne $_.$name } | ForEach-Object { [double]$_.$name }) }
        [pscustomobject]@{
            Host         = $catalog[$key].Name
            MedianMs     = Get-Median @($allRuns | ForEach-Object { [double]$_.STARTUP_MS })
            P90Ms        = Get-P90 @($allRuns | ForEach-Object { [double]$_.STARTUP_MS })
            FirstLaunch  = [math]::Round($summaries[0].firstLaunchMs, 0)
            AppCode      = & $column 'MARK_main'
            Window       = & $column 'MARK_windowCreated'
            WebViewReady = & $column 'MARK_webviewControllerCreated'
            Navigate     = & $column 'MARK_navigationStarted'
            MemoryMb     = & $column 'PRIVATE_MB'
            InstallMb    = $summaries[0].installSizeMB
        }
    }

    $lines += @(
        "### $fixture",
        '',
        "$Rounds rounds x $Runs warm runs per host, order reversed every other round. $($summaries[0].machine), $(Get-Date -Format 'yyyy-MM-dd').",
        '',
        '| Host | Median | p90 | First launch | App code starts | Window created | WebView2 ready | Navigate | Memory | Build output |',
        '|---|---|---|---|---|---|---|---|---|---|'
    )
    foreach ($row in $rows) {
        $lines += "| $($row.Host) | **$($row.MedianMs) ms** | $($row.P90Ms) ms | $($row.FirstLaunch) ms | $($row.AppCode) | $($row.Window) | $($row.WebViewReady) | $($row.Navigate) | $($row.MemoryMb) MB | $($row.InstallMb) MB |"
    }
    $lines += ''
}

$table = $lines -join [Environment]::NewLine
$tableFile = Join-Path $OutDir 'compare.md'
Set-Content $tableFile $table -Encoding utf8
Write-Host ""
Write-Host $table
Write-Host ""
Write-Host "Saved to $tableFile" -ForegroundColor Green
