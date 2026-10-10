using System;
using System.IO;
using System.Linq;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

var src = """
flowchart TD
    M[Message] --> D{Type?}
    D -->|Create| H1[Create handler]
    D -->|Update| H2[Update handler]
    D -->|else| U[Unknown handler]
""";
var c = MermaidRecognizer.Parse(src);
FlowchartSugiyamaLayoutEngine.TryLayout(c, out var layout);
var d = layout.NodeBounds.First(b => b.NodeId == "D");
double leftX = d.CenterX - d.Width / 2.0;
double rightX = d.CenterX + d.Width / 2.0;
double bottomY = d.CenterY - d.Height / 2.0;
// can't see OldX from layout result - use dagre placement directly
var c2 = MermaidRecognizer.Parse(src);
FlowchartDagrePlacement.TryPlace(c2, out var dagre);
foreach (var n in dagre.NodeBounds.OrderByDescending(b => b.CenterY))
    Console.WriteLine($"dagre {n.NodeId}: x={n.CenterX:0} y={n.CenterY:0}");
foreach (var n in layout.NodeBounds.OrderByDescending(b => b.CenterY))
    Console.WriteLine($"{n.NodeId}: x={n.CenterX:0} y={n.CenterY:0} w={n.Width:0} h={n.Height:0}");
foreach (var r in layout.EdgeRoutes.Where(r => r.SourceId == "D"))
{
    Console.WriteLine($"ROUTE D->{r.TargetId} exit={r.ExitSide} entry={r.EntrySide} pts={r.Points.Count}");
    foreach (var p in r.Points) Console.WriteLine($"  {p.X:0},{p.Y:0}");
    var p0 = r.Points[0];
    string port = Math.Abs(p0.X - leftX) < 2 ? "LEFT" : Math.Abs(p0.X - rightX) < 2 ? "RIGHT" : Math.Abs(p0.Y - bottomY) < 2 ? "BOTTOM" : "OTHER";
    Console.WriteLine($"  start port: {port}");
}
var svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(c);
Directory.CreateDirectory("/opt/cursor/artifacts");
File.WriteAllText("/opt/cursor/artifacts/type-decision.svg", svg);
