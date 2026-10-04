$edits=@{
 'Core/Geometry/RTree/RectangleNode.cs'=@{'.Select(node => node.UserData)'='.Select(node => node.UserData!)';'.Select(n => n.UserData)'='.Select(n => n.UserData!)'};
 'Core/Geometry/RTree/IntervalNode.cs'=@{'.Select(node => node.UserData)'='.Select(node => node.UserData!)';'.Select(n => n.UserData)'='.Select(n => n.UserData!)'};
 'Layout/Layered/RefinerBetweenTwoLayers.cs'=@{'bottomSite = topSiteP.Next;'='bottomSite = topSiteP.Next ?? throw new ArgumentException("The top site must have a successor.", nameof(topSiteP));';'currentBottomSite = topSiteP.Next;'='currentBottomSite = bottomSite;'};
 'Miscellaneous/LayoutEditing/RouterBetweenTwoNodes.cs'=@{'public Port SourcePort'='public Port? SourcePort';'public Port TargetPort'='public Port? TargetPort'};
 'Routing/InteractiveEdgeRouter.cs'=@{'out Polyline tmpTargetTight'='out Polyline? tmpTargetTight'};
 'Routing/Rectilinear/MsmtRectilinearPath.cs'=@{'out VertexEntry lastEntry'='[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out VertexEntry? lastEntry'};
 'Routing/Spline/Bundling/Station.cs'=@{'internal CdtTriangle CdtTriangle;'='internal CdtTriangle? CdtTriangle;'};
 'Routing/Rectilinear/ObstaclePortEntrance.cs'=@{'.Select(node => node.UserData)'='.Select(node => node.UserData!)'}
}
foreach($relative in $edits.Keys) {
    $file=Join-Path $PWD ('new_src/MermaidRecognition.Layout/Sugiyama/'+$relative)
    $content=[IO.File]::ReadAllText($file)
    foreach($key in $edits[$relative].Keys) { $content=$content.Replace($key,$edits[$relative][$key]) }
    [IO.File]::WriteAllText($file,$content)
}
$file=Join-Path $PWD 'new_src/MermaidRecognition.Layout/Sugiyama/Core/Geometry/Curves/Curve.cs'
$content=[IO.File]::ReadAllText($file)
$content=$content.Replace('FindCorner(Site a, out Site b, out Site c)', 'FindCorner(Site a, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Site? b, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Site? c)')
$content=$content.Replace('            if (b.Next == null)', '            if (b == null || b.Next == null)')
[IO.File]::WriteAllText($file,$content)
$warnings=Get-Content (Join-Path $PSScriptRoot 'warnings-build-6.log') | Where-Object {$_ -match ': warning CS8600:'} | Sort-Object -Unique
foreach($warning in $warnings) {
    if($warning -match '^(.+)\((\d+),\d+\): warning') {
        $file=$Matches[1]; $index=[int]$Matches[2]-1; $lines=[IO.File]::ReadAllLines($file)
        $lines[$index]=[regex]::Replace($lines[$index],'out ([\w.]+(?:<[^>]+>)?(?:\[\])?) (\w+)','out $1? $2')
        [IO.File]::WriteAllText($file + ".codex-tmp", ($lines -join "`r`n") + "`r`n"); [IO.File]::Move($file + ".codex-tmp", $file, $true)
    }
}
