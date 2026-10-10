using System;
using System.IO;
using System.Linq;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

var src = """
flowchart TD
    Q[Queue] --> L{Load balance}
    L --> C1[Instance 1]
    L --> C2[Instance 2]
    L --> C3[Instance 3]
    C1 --> DL[Shared DLQ policy]
    C2 --> DL
    C3 --> DL
""";
var c = MermaidRecognizer.Parse(src);
FlowchartSugiyamaLayoutEngine.TryLayout(c, out var layout);
foreach (var n in layout.NodeBounds.OrderByDescending(b => b.CenterY))
{
    double top = n.CenterY + n.Height / 2.0;
    double bottom = n.CenterY - n.Height / 2.0;
    Console.WriteLine($"{n.NodeId}: cy={n.CenterY:0} top={top:0} bottom={bottom:0} h={n.Height:0}");
}

var byRank = layout.NodeBounds
    .OrderByDescending(b => b.CenterY)
    .GroupBy(b => Math.Round(b.CenterY, 0))
    .ToList();
Console.WriteLine("--- clear gaps ---");
var sorted = layout.NodeBounds.OrderByDescending(b => b.CenterY).ToList();
var ranks = new List<(string id, double top, double bottom)>();
foreach (var n in sorted)
{
    ranks.Add((n.NodeId, n.CenterY + n.Height / 2.0, n.CenterY - n.Height / 2.0));
}
// group by approximate rank
var rankGroups = layout.NodeBounds
    .GroupBy(n => n.CenterY)
    .OrderByDescending(g => g.Key)
    .ToList();
for (int i = 0; i < rankGroups.Count - 1; i++)
{
    double bottomOfUpper = rankGroups[i].Min(n => n.CenterY - n.Height / 2.0);
    double topOfLower = rankGroups[i + 1].Max(n => n.CenterY + n.Height / 2.0);
    double clearGap = bottomOfUpper - topOfLower;
    Console.WriteLine($"rank {i}->{i+1}: clear gap = {clearGap:0}px");
}

var svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(c);
Directory.CreateDirectory("/opt/cursor/artifacts");
File.WriteAllText("/opt/cursor/artifacts/load-balance.svg", svg);
