<#
.SYNOPSIS
    Writes the benchmark documents to benchmarks/fixtures: Markdown-like text with headings, lists,
    tables and code, at 500, 5,000 (the 5k budget document) and 50,000 lines.
.EXAMPLE
    pwsh benchmarks/New-Fixtures.ps1
#>
[CmdletBinding()]
param(
    [string] $OutDir = (Join-Path $PSScriptRoot 'fixtures')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null

function New-Document([int] $lines) {
    $sb = [System.Text.StringBuilder]::new()
    $section = 0
    while ($true) {
        $section++
        $block = @(
            "## Section $section",
            '',
            "This paragraph describes section $section with **bold**, *italic*, ``inline code`` and a [link](https://example.com/$section).",
            'It wraps onto a second line to look like real prose in a README file.',
            '',
            '- First item',
            '- Second item with `code`',
            '  - Nested item',
            '',
            '| Name | Value | Notes |',
            '|------|------:|-------|',
            "| alpha | $section | first |",
            "| beta | $($section * 2) | second |",
            '',
            '```csharp',
            "public static int Section$section(int value)",
            '{',
            "    return value * $section;",
            '}',
            '```',
            ''
        )
        foreach ($line in $block) {
            [void] $sb.Append($line).Append("`n")
            if (--$lines -le 0) { return $sb.ToString() }
        }
    }
}

foreach ($fixture in @(@{ Name = 'readme-500.md'; Lines = 500 }, @{ Name = 'readme-5k.md'; Lines = 5000 }, @{ Name = 'readme-50k.md'; Lines = 50000 })) {
    $path = Join-Path $OutDir $fixture.Name
    [System.IO.File]::WriteAllText($path, "# Benchmark fixture`n`n" + (New-Document ($fixture.Lines - 2)))
    Write-Host ("{0}: {1:N0} lines, {2:N0} KB" -f $fixture.Name, $fixture.Lines, ((Get-Item $path).Length / 1KB))
}
