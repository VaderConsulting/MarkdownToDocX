$edits = @{
 'Core/Geometry/Site.cs' = @{ 'Site prev;'='Site? prev;'; 'Site next;'='Site? next;'; 'public Site Previous'='public Site? Previous'; 'public Site Next'='public Site? Next' };
 'Core/Layout/Cluster.cs' = @{ 'ICurve collapsedBoundary;'='ICurve? collapsedBoundary;'; 'public ICurve CollapsedBoundary'='public ICurve? CollapsedBoundary' };
 'Core/Layout/Label.cs' = @{ 'public IList<Point> InnerPoints'='public IList<Point>? InnerPoints'; 'public IList<Point> OuterPoints'='public IList<Point>? OuterPoints' };
 'Core/Layout/EdgeGeometry.cs' = @{ 'public Arrowhead SourceArrowhead'='public Arrowhead? SourceArrowhead'; 'public Arrowhead TargetArrowhead'='public Arrowhead? TargetArrowhead'; 'public Port SourcePort'='public Port? SourcePort'; 'public Port TargetPort'='public Port? TargetPort' };
 'Core/Layout/Edge.cs' = @{ 'public Port SourcePort'='public Port? SourcePort'; 'public Port TargetPort'='public Port? TargetPort' };
 'Core/Geometry/RTree/RectangleNode.cs' = @{ 'public TData UserData'='public TData? UserData' };
 'Core/Geometry/RTree/IntervalNode.cs' = @{ 'public TData UserData'='public TData? UserData' };
 'Core/Geometry/RTree/RTree.cs' = @{ 'out TData intersectedLeaf'='out TData? intersectedLeaf' };
 'Routing/ConstrainedDelaunayTriangulation/CdtEdge.cs' = @{ 'CdtTriangle ccwTriangle;'='CdtTriangle? ccwTriangle;'; 'CdtTriangle cwTriangle;'='CdtTriangle? cwTriangle;'; 'public CdtTriangle CcwTriangle'='public CdtTriangle? CcwTriangle'; 'public CdtTriangle CwTriangle'='public CdtTriangle? CwTriangle' };
 'Routing/Spline/Bundling/CdtThreader.cs' = @{ 'CdtEdge currentPiercedEdge;'='CdtEdge? currentPiercedEdge;'; 'internal CdtEdge CurrentPiercedEdge'='internal CdtEdge? CurrentPiercedEdge' };
 'Routing/Spline/ConeSpanner/ConeSpanner.cs' = @{ 'Polyline BorderPolyline { get; set; }'='Polyline? BorderPolyline { get; set; }' };
 'Routing/Rectilinear/MsmtRectilinearPath.cs' = @{ 'VertexEntry bestEntry = null;'='VertexEntry? bestEntry = null;'; 'ref VertexEntry bestEntry'='ref VertexEntry? bestEntry'; 'private VertexEntry GetPathStage'='private VertexEntry? GetPathStage' };
 'Routing/Rectilinear/LookaheadScan.cs' = @{ 'internal int Compare(Point? lhs, Point? rhs) { if (lhs is null) return rhs is null ? 0 : -1; if (rhs is null) return 1; return scanDirection.CompareScanCoord(lhs, rhs); }'='internal int Compare(Point lhs, Point rhs) => scanDirection.CompareScanCoord(lhs, rhs);' };
 'Layout/Layered/Ordering/ConstrainedOrdering.cs' = @{ '        void AllocateXPositionsClone() {'="        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(xPositionsClone))]`r`n        void AllocateXPositionsClone() {" };
 'Routing/SplineRouter.cs' = @{ 'obstacleShapes.Select(sh => shapesToTightLooseCouples[sh].LooseShape.BoundaryCurve as Polyline)'='obstacleShapes.Select(sh => (Polyline)shapesToTightLooseCouples[sh].LooseShape.BoundaryCurve)'; 'looseShape.Children.Select(c => c.BoundaryCurve as Polyline)'='looseShape.Children.Select(c => (Polyline)c.BoundaryCurve)' }
}
foreach ($relative in $edits.Keys) {
    $file = Join-Path $PWD ('new_src/MermaidRecognition.Layout/Sugiyama/'+$relative)
    if (-not [IO.File]::Exists($file)) { Write-Output ('Missing optional file: '+$relative); continue }
    $content = [IO.File]::ReadAllText($file)
    foreach ($key in $edits[$relative].Keys) { $content = $content.Replace($key,$edits[$relative][$key]) }
    [IO.File]::WriteAllText($file,$content)
}
$file=Join-Path $PWD 'new_src/MermaidRecognition.Layout/Sugiyama/Routing/InteractiveEdgeRouter.cs'
$content=[IO.File]::ReadAllText($file)
$content=[regex]::Replace($content,'\b(Polyline|Port|VisibilityVertex) (_sourceTightPolyline|SourceTightPolyline|SourceLoosePolyline|targetTightPolyline|TargetTightPolyline|targetLoosePolyline|TargetLoosePolyline|sourcePort|SourcePort|targetPort|TargetPort|_sourceVisibilityVertex|SourceVisibilityVertex|targetVisibilityVertex|TargetVisibilityVertex)\b','$1? $2')
[IO.File]::WriteAllText($file,$content)
$file=Join-Path $PWD 'new_src/MermaidRecognition.Layout/Sugiyama/Routing/Spline/Bundling/FlipSwitcher.cs'
$content=[IO.File]::ReadAllText($file)
$start=$content.IndexOf('        void FindPointsOnPolyline(')
$end=$content.IndexOf('        bool PolylinePointsAreInForwardOrder', $start)
$replacement=@"
        void FindPointsOnPolyline(Polyline polyline, Point first, Point last,
            out PolylinePoint ppFirst, out PolylinePoint ppLast, out bool forwardOrder) {
            PolylinePoint? firstPoint = null;
            PolylinePoint? lastPoint = null;
            forwardOrder = false;
            for (PolylinePoint pp = polyline.StartPoint; pp != null; pp = pp.Next) {
                if (pp.Point == first) firstPoint = pp;
                if (pp.Point == last) lastPoint = pp;
                if (firstPoint != null && lastPoint == null) forwardOrder = true;
                if (firstPoint == null && lastPoint != null) forwardOrder = false;
            }
            ppFirst = firstPoint ?? throw new System.InvalidOperationException("First point is not on the polyline.");
            ppLast = lastPoint ?? throw new System.InvalidOperationException("Last point is not on the polyline.");
        }

"@
$content=$content.Substring(0,$start)+$replacement+"`r`n"+$content.Substring($end)
[IO.File]::WriteAllText($file,$content)
