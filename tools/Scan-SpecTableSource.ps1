$ErrorActionPreference = 'Stop'

function Get-TableRowColumnCount
{
    param([string]$Row)

    if ($Row -notmatch '^\|')
    {
        return -1
    }

    $Stripped = [regex]::Replace($Row, '`[^`]*`', { param($Match) ('`' + ('X' * ($Match.Value.Length - 2)) + '`') })
    $Stripped = $Stripped.Trim()
    if ($Stripped.StartsWith('|'))
    {
        $Stripped = $Stripped.Substring(1)
    }
    if ($Stripped.EndsWith('|'))
    {
        $Stripped = $Stripped.Substring(0, $Stripped.Length - 1)
    }

    $Parts = $Stripped -split '(?<!\\)\|'
    return $Parts.Count
}

function Test-SeparatorRow
{
    param([string]$Row)

    $Stripped = [regex]::Replace($Row, '`[^`]*`', '')
    return $Stripped -match '^\|\s*[-:]+\s*(\|\s*[-:]+\s*)+\|?\s*$'
}

$SpecPath = Join-Path $PSScriptRoot '..\spec.md'
$Lines = [System.IO.File]::ReadAllLines($SpecPath)
$InCodeFence = $false
$InTable = $false
$TableStart = 0
$ExpectedColumns = 0
$HasSeparator = $false
$Issues = @()
$BlankBetweenRows = @()

for ($I = 0; $I -lt $Lines.Count; $I++)
{
    $Line = $Lines[$I]
    if ($Line -match '^```')
    {
        $InCodeFence = -not $InCodeFence
        $InTable = $false
        continue
    }

    if ($InCodeFence)
    {
        continue
    }

    if ($Line -match '^\|')
    {
        $ColumnCount = Get-TableRowColumnCount $Line
        if (-not $InTable)
        {
            $InTable = $true
            $TableStart = $I + 1
            $ExpectedColumns = $ColumnCount
            $HasSeparator = Test-SeparatorRow $Line
            continue
        }

        if (Test-SeparatorRow $Line)
        {
            if ($ColumnCount -ne $ExpectedColumns)
            {
                $Issues += "Separator column mismatch at line $($I + 1): $ColumnCount vs header $ExpectedColumns (table from $TableStart)"
            }
            $HasSeparator = $true
            continue
        }

        if ($ColumnCount -ne $ExpectedColumns)
        {
            $Issues += "Row column mismatch at line $($I + 1): $ColumnCount vs $ExpectedColumns (table from $TableStart)"
        }

        continue
    }

    if ($InTable)
    {
        if ($Line -match '^\s*$')
        {
            $Next = $I + 1
            while ($Next -lt $Lines.Count -and $Lines[$Next] -match '^\s*$')
            {
                $Next++
            }

            if ($Next -lt $Lines.Count -and $Lines[$Next] -match '^\|')
            {
                $BlankBetweenRows += "Blank at line $($I + 1) splits table from $TableStart (continues line $($Next + 1))"
            }
            elseif (-not $HasSeparator)
            {
                $Issues += "Table from line $TableStart ended without separator before line $($I + 1)"
            }

            $InTable = $false
            continue
        }

        if ($Line -match '^\*\*[^*]+\*\*')
        {
            if (-not $HasSeparator)
            {
                $Issues += "Table from line $TableStart interrupted by heading at line $($I + 1) without separator"
            }
            $InTable = $false
            continue
        }

        if ($Line -match '^\S')
        {
            if (-not $HasSeparator)
            {
                $Issues += "Table from line $TableStart interrupted at line $($I + 1) without separator"
            }
            $InTable = $false
        }
    }
}

Write-Host "Column mismatches: $($Issues.Count)"
$Issues | ForEach-Object { Write-Host "  $_" }
Write-Host "Blank lines splitting table continuations: $($BlankBetweenRows.Count)"
$BlankBetweenRows | ForEach-Object { Write-Host "  $_" }
