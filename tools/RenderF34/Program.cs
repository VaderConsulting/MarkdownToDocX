using System;
using System.IO;
using System.Linq;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

var src = """
flowchart LR
A[Receive] --> B{OK?}
B -->|Yes| C[Done]
B -->|No| D[Retry]
D --> A
""";
var c = MermaidRecognizer.Parse(src);
FlowchartSugiyamaLayoutEngine.TryLayout(c, out var layout);
foreach (var n in layout.NodeBounds.OrderBy(b => b.CenterX).ThenBy(b => b.CenterY))
    Console.WriteLine($"{n.NodeId}: x={n.CenterX:0} y={n.CenterY:0} w={n.Width:0} h={n.Height:0}");
foreach (var r in layout.EdgeRoutes)
{
    Console.WriteLine($"ROUTE {r.SourceId}->{r.TargetId} exit={r.ExitSide} entry={r.EntrySide} pts={r.Points.Count}");
    foreach (var p in r.Points) Console.WriteLine($"  {p.X:0},{p.Y:0}");
}
var svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(c);
Directory.CreateDirectory("/opt/cursor/artifacts");
File.WriteAllText("/opt/cursor/artifacts/f34-retry-loop.svg", svg);
