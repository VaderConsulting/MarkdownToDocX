# Tightens docs/currentstatus.md: removes blank lines between table rows, inside code fences, and loose list items.
$ErrorActionPreference = 'Stop'
$Utf8 = [System.Text.UTF8Encoding]::new($false)

function Test-ListMarker
{
    param([string]$Line)
    return $Line -match '^(\s*)([-*+]|\d+\.)\s'
}

function Get-LeadingIndent
{
    param([string]$Line)
    if ($Line -match '^(\s*)')
    {
        return $Matches[1].Length
    }
    return 0
}

function Get-ListItemBaseIndent
{
    param(
        [string[]]$AllLines,
        [int]$Index
    )

    for ($J = $Index; $J -ge 0; $J--)
    {
        if ($AllLines[$J] -match '^\s*$')
        {
            continue
        }

        if (Test-ListMarker $AllLines[$J])
        {
            return Get-LeadingIndent $AllLines[$J]
        }

        if ($AllLines[$J] -match '^\s{2,}\S')
        {
            continue
        }

        break
    }

    return $null
}

function Test-ListContinuation
{
    param(
        [string]$Line,
        [int]$BaseIndent
    )

    if ($Line -match '^\s*$')
    {
        return $false
    }

    if (Test-ListMarker $Line)
    {
        return $false
    }

    return (Get-LeadingIndent $Line) -gt $BaseIndent
}

function Test-ShouldRemoveBlankBetweenListItems
{
    param(
        [string[]]$AllLines,
        [int]$BlankIndex
    )

    $Prev = $BlankIndex - 1
    while ($Prev -ge 0 -and $AllLines[$Prev] -match '^\s*$')
    {
        $Prev--
    }

    $Next = $BlankIndex + 1
    while ($Next -lt $AllLines.Count -and $AllLines[$Next] -match '^\s*$')
    {
        $Next++
    }

    if ($Prev -lt 0 -or $Next -ge $AllLines.Count)
    {
        return $false
    }

    $PrevLine = $AllLines[$Prev]
    $NextLine = $AllLines[$Next]
    if (-not (Test-ListMarker $NextLine))
    {
        return $false
    }

    $NextIndent = Get-LeadingIndent $NextLine
    if (Test-ListMarker $PrevLine)
    {
        $PrevIndent = Get-LeadingIndent $PrevLine
        if ($PrevIndent -eq $NextIndent)
        {
            return $true
        }

        $BaseIndent = Get-ListItemBaseIndent $AllLines $Prev
        if ($null -ne $BaseIndent -and $NextIndent -le $BaseIndent)
        {
            return $true
        }

        return $false
    }

    $BaseIndent = Get-ListItemBaseIndent $AllLines $Prev
    if ($null -eq $BaseIndent)
    {
        return $false
    }

    if ($NextIndent -le $BaseIndent -and (Test-ListContinuation $PrevLine $BaseIndent -or (Get-LeadingIndent $PrevLine) -gt $BaseIndent))
    {
        return $true
    }

    return $false
}

function Test-ShouldRemoveBlankBetweenTableRows
{
    param(
        [string[]]$AllLines,
        [int]$BlankIndex
    )

    $Prev = $BlankIndex - 1
    while ($Prev -ge 0 -and $AllLines[$Prev] -match '^\s*$')
    {
        $Prev--
    }

    $Next = $BlankIndex + 1
    while ($Next -lt $AllLines.Count -and $AllLines[$Next] -match '^\s*$')
    {
        $Next++
    }

    if ($Prev -lt 0 -or $Next -ge $AllLines.Count)
    {
        return $false
    }

    return ($AllLines[$Prev] -match '^\|' -and $AllLines[$Next] -match '^\|')
}

function Fix-StatusSpacing
{
    param([string[]]$Lines)

    $InCodeFence = $false
    $Result = New-Object System.Collections.Generic.List[string]
    $RemovedBetweenListItems = 0
    $RemovedBetweenTableRows = 0
    $RemovedInsideCodeFence = 0
    $CollapsedExtraBlanks = 0

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
            if ($Line -match '^\s*$')
            {
                $RemovedInsideCodeFence++
                continue
            }

            $Result.Add($Line.TrimEnd())
            continue
        }

        if ($Line -match '^\s*$')
        {
            if (Test-ShouldRemoveBlankBetweenTableRows $Lines $I)
            {
                $RemovedBetweenTableRows++
                continue
            }

            if (Test-ShouldRemoveBlankBetweenListItems $Lines $I)
            {
                $RemovedBetweenListItems++
                continue
            }

            if ($Result.Count -gt 0 -and $Result[$Result.Count - 1] -match '^\s*$')
            {
                $CollapsedExtraBlanks++
                continue
            }

            $Result.Add('')
            continue
        }

        $Result.Add($Line.TrimEnd())
    }

    return [PSCustomObject]@{
        Lines = $Result.ToArray()
        RemovedBetweenListItems = $RemovedBetweenListItems
        RemovedBetweenTableRows = $RemovedBetweenTableRows
        RemovedInsideCodeFence = $RemovedInsideCodeFence
        CollapsedExtraBlanks = $CollapsedExtraBlanks
    }
}

$TargetPath = Join-Path $PSScriptRoot '..\docs\currentstatus.md'
$OriginalLines = [System.IO.File]::ReadAllLines($TargetPath)
$FixResult = Fix-StatusSpacing -Lines $OriginalLines

if ($FixResult.Lines -join "`n" -ne ($OriginalLines -join "`n"))
{
    [System.IO.File]::WriteAllText($TargetPath, ($FixResult.Lines -join "`n") + "`n", $Utf8)
    Write-Host "Removed $($FixResult.RemovedBetweenTableRows) blank line(s) between table rows."
    Write-Host "Removed $($FixResult.RemovedBetweenListItems) blank line(s) between list items."
    Write-Host "Removed $($FixResult.RemovedInsideCodeFence) blank line(s) inside code fences."
    Write-Host "Collapsed $($FixResult.CollapsedExtraBlanks) extra consecutive blank line(s)."
    Write-Host "Updated: $TargetPath"
}
else
{
    Write-Host "No spacing changes needed: $TargetPath"
}
