# Repairs common Markdown table issues in spec.md (blank lines between rows, bare --- cells, truncated alias cells).
$ErrorActionPreference = 'Stop'
$Utf8 = [System.Text.UTF8Encoding]::new($false)

function Get-TableRowColumnCount
{
    param([string]$Row)

    if ($Row -notmatch '^\|')
    {
        return -1
    }

    $Stripped = [regex]::Replace($Row, '`[^`]*`', { param($Match) ('`' + ('X' * ($Match.Value.Length - 2)) + '`') })
    $Stripped = $Stripped.Trim().Trim('|')
    return ($Stripped -split '(?<!\\)\|').Count
}

function Test-SeparatorRow
{
    param([string]$Row)

    $Stripped = [regex]::Replace($Row, '`[^`]*`', '')
    return $Stripped -match '^\|\s*[-:]+\s*(\|\s*[-:]+\s*)+\|?\s*$'
}

function Fix-TableDataCells
{
    param([string]$Line)

    if ($Line -notmatch '^\|' -or (Test-SeparatorRow $Line))
    {
        return $Line
    }

    $Updated = $Line
    $Updated = [regex]::Replace($Updated, '\| --- \| starts with', '| `---` | starts with')
    $Updated = $Updated -replace "\| 'lined-rectangle', 'lined-process', 'lin-proc', 'shaded-p\.\.\. \|", "| `'lined-rectangle', 'lined-process', 'lin-proc', 'shaded-process'` |"
    return $Updated
}

function Fix-SpecTables
{
    param([string[]]$Lines)

    $InCodeFence = $false
    $Result = New-Object System.Collections.Generic.List[string]
    $RemovedBlankLines = 0
    $FixedCells = 0

    for ($I = 0; $I -lt $Lines.Count; $I++)
    {
        $Line = $Lines[$I]
        if ($Line -match '^```')
        {
            $InCodeFence = -not $InCodeFence
            $Result.Add($Line)
            continue
        }

        if ($InCodeFence)
        {
            $Result.Add($Line)
            continue
        }

        if ($Line -match '^\|')
        {
            $FixedLine = Fix-TableDataCells -Line $Line
            if ($FixedLine -ne $Line)
            {
                $FixedCells++
            }
            $Result.Add($FixedLine)
            continue
        }

        if ($Line -match '^\s*$')
        {
            $Prev = $I - 1
            while ($Prev -ge 0 -and $Lines[$Prev] -match '^\s*$')
            {
                $Prev--
            }

            $Next = $I + 1
            while ($Next -lt $Lines.Count -and $Lines[$Next] -match '^\s*$')
            {
                $Next++
            }

            if ($Prev -ge 0 -and $Next -lt $Lines.Count -and $Lines[$Prev] -match '^\|' -and $Lines[$Next] -match '^\|')
            {
                $PrevColumns = Get-TableRowColumnCount $Lines[$Prev]
                $NextColumns = Get-TableRowColumnCount $Lines[$Next]
                if ($PrevColumns -gt 0 -and $PrevColumns -eq $NextColumns)
                {
                    $RemovedBlankLines++
                    continue
                }
            }

            $Result.Add('')
            continue
        }

        $Result.Add($Line.TrimEnd())
    }

    return [PSCustomObject]@{
        Lines = $Result.ToArray()
        RemovedBlankLines = $RemovedBlankLines
        FixedCells = $FixedCells
    }
}

$TargetPath = Join-Path $PSScriptRoot '..\spec.md'
$OriginalLines = [System.IO.File]::ReadAllLines($TargetPath)
$FixResult = Fix-SpecTables -Lines $OriginalLines

if ($FixResult.Lines -join "`n" -ne ($OriginalLines -join "`n"))
{
    [System.IO.File]::WriteAllText($TargetPath, ($FixResult.Lines -join "`n") + "`n", $Utf8)
    Write-Host "Removed $($FixResult.RemovedBlankLines) blank line(s) inside tables."
    Write-Host "Fixed $($FixResult.FixedCells) table cell(s)."
    Write-Host "Updated: $TargetPath"
}
else
{
    Write-Host "No table fixes needed: $TargetPath"
}
