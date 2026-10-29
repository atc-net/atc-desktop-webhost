<#
.SYNOPSIS
    Writes the benchmark documents to benchmarks/fixtures:
    - text-1kb.txt and text-10mb.txt: plain text with random line lengths (the default startup tests).
    - readme-500.md, readme-5k.md, readme-50k.md: Markdown-like text with headings, lists, tables and code.
    The text files use a fixed seed, so every machine gets the same bytes. text-10mb.txt is not committed.
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

# Plain text with random line lengths: mostly 0–120 characters, some up to 400, a few up to 4,000.
function New-Text([long] $bytes, [int] $seed) {
    $random = [System.Random]::new($seed)
    $words = 'lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua 0123456789 the quick brown fox jumps over the lazy dog'.Split(' ')
    $pool = [System.Text.StringBuilder]::new()
    while ($pool.Length -lt 65536) { [void] $pool.Append($words[$random.Next($words.Length)]).Append(' ') }
    $pool = $pool.ToString()

    $sb = [System.Text.StringBuilder]::new([int] $bytes + 4096)
    $line = 0
    while ($sb.Length -lt $bytes) {
        $line++
        $roll = $random.NextDouble()
        $length = if ($roll -lt 0.9) { $random.Next(0, 121) } elseif ($roll -lt 0.99) { $random.Next(121, 401) } else { $random.Next(401, 4001) }
        $remaining = [int] ($bytes - $sb.Length - 1)
        if ($length -gt $remaining) { $length = $remaining }
        if ($length -gt 0) {
            $prefix = "$line "
            $start = $random.Next(0, $pool.Length - $length)
            $take = $length - $prefix.Length
            [void] $sb.Append($prefix)
            if ($take -gt 0) { [void] $sb.Append($pool.Substring($start, $take)) }
        }

        [void] $sb.Append("`n")
    }

    return $sb.ToString(0, [int] $bytes)
}

foreach ($fixture in @(@{ Name = 'text-1kb.txt'; Bytes = 1KB; Seed = 1 }, @{ Name = 'text-10mb.txt'; Bytes = 10MB; Seed = 10 })) {
    $path = Join-Path $OutDir $fixture.Name
    [System.IO.File]::WriteAllText($path, (New-Text $fixture.Bytes $fixture.Seed))
    $content = [System.IO.File]::ReadAllText($path)
    Write-Host ("{0}: {1:N0} lines, {2:N0} KB" -f $fixture.Name, ($content.Split("`n").Count - 1), ((Get-Item $path).Length / 1KB))
}
