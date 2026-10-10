using System;
using System.IO;
using System.Linq;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

var src = """
flowchart TD
    P[Purge request] --> V{Authorised + scoped?}
    V -->|no| X[Reject / audit alert]
    V -->|yes| E[Export optional archive]
    E --> R[Remove messages]
    R --> L[Log counts + ticket ref]
""";
var c = MermaidRecognizer.Parse(src);
FlowchartDagrePlacement.TryPlace(c, out var dagre);
foreach (var n in dagre.NodeBounds.OrderByDescending(b => b.CenterY))
    Console.WriteLine($"dagre {n.NodeId}: x={n.CenterX:0} y={n.CenterY:0}");
FlowchartSugiyamaLayoutEngine.TryLayout(c, out var layout);
foreach (var n in layout.NodeBounds.OrderByDescending(b => b.CenterY))
    Console.WriteLine($"{n.NodeId}: x={n.CenterX:0} y={n.CenterY:0} h={n.Height:0}");
var ordered = layout.NodeBounds.OrderByDescending(b => b.CenterY).ToList();
for (int i = 0; i < ordered.Count - 1; i++)
{
    double bottom = ordered[i].CenterY - ordered[i].Height / 2.0;
    double top = ordered[i + 1].CenterY + ordered[i + 1].Height / 2.0;
    Console.WriteLine($"gap {ordered[i].NodeId}->{ordered[i+1].NodeId}: {bottom - top:0}");
}
var svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(c);
Directory.CreateDirectory("/opt/cursor/artifacts");
File.WriteAllText("/opt/cursor/artifacts/purge-request.svg", svg);
