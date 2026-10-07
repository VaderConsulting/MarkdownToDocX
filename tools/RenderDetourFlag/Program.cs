using System;
using System.IO;
using System.Linq;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

var src = """
flowchart TD
    M[Message] --> D{Detour flag?}
    D -->|on| X[Inspection / shadow]
    X --> R[Rejoin main path]
    D -->|off| R
""";
var c = MermaidRecognizer.Parse(src);
FlowchartDagrePlacement.TryPlace(c, out var dagre);
foreach (var n in dagre.NodeBounds.OrderByDescending(b => b.CenterY))
    Console.WriteLine($"dagre {n.NodeId}: x={n.CenterX:0} y={n.CenterY:0}");
FlowchartSugiyamaLayoutEngine.TryLayout(c, out var layout);
foreach (var n in layout.NodeBounds.OrderByDescending(b => b.CenterY))
    Console.WriteLine($"{n.NodeId}: x={n.CenterX:0} y={n.CenterY:0}");
foreach (var r in layout.EdgeRoutes)
{
    Console.WriteLine($"ROUTE {r.SourceId}->{r.TargetId} exit={r.ExitSide} entry={r.EntrySide} pts={r.Points.Count}");
    foreach (var p in r.Points) Console.WriteLine($"  {p.X:0},{p.Y:0}");
}
var svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(c);
Directory.CreateDirectory("/opt/cursor/artifacts");
File.WriteAllText("/opt/cursor/artifacts/detour-flag.svg", svg);
