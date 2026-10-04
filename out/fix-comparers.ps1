$warningLog = Join-Path $PSScriptRoot 'warnings-unique.txt'
$comparerFiles = @(Get-Content $warningLog | Where-Object { $_ -match 'warning CS876[79]:' } | ForEach-Object { if ($_ -match '^(.+)\(\d+,\d+\):') { $Matches[1] } } | Sort-Object -Unique)
foreach ($file in $comparerFiles) {
    $content = [IO.File]::ReadAllText($file)
    $pattern = '(?m)(^[ \t]*(?:public |internal )?int (?:IComparer<[^>]+>\.)?Compare(?:To)?\()([^\)]+)(\)\s*)(=>\s*([^;]+);|\{)'
    $content = [regex]::Replace($content,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($match)
        $parts = $match.Groups[2].Value.Split(',')
        $names = @()
        $parameters = foreach ($part in $parts) {
            $tokens = $part.Trim() -split '\s+'
            $names += $tokens[-1]
            $type = $tokens[0].TrimEnd('?') + '?'
            $type + ' ' + $tokens[-1]
        }
        $signature = $match.Groups[1].Value + ($parameters -join ', ') + ')'
        if ($names.Count -eq 1) { $guard = 'if (' + $names[0] + ' is null) return 1;' }
        else { $guard = 'if (' + $names[0] + ' is null) return ' + $names[1] + ' is null ? 0 : -1; if (' + $names[1] + ' is null) return 1;' }
        if ($match.Groups[4].Value.StartsWith('=>')) { $signature + ' { ' + $guard + ' return ' + $match.Groups[5].Value + '; }' }
        else { $signature + " {`r`n            " + $guard }
    })
    [IO.File]::WriteAllText($file,$content)
}
