using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartCellComponentWrappingTests
{
    [Test]
    public void F153_Layout_StaysWithinWordUsableWidth()
    {
        string source = ReadScenario("F153");
        var contract = MermaidRecognizer.Parse(source);
        Assert.That(
            FlowchartSugiyamaLayoutEngine.TryLayout(
                contract,
                new FlowchartRenderContext { UseCellRegions = true },
                out FlowchartLayoutResult layout),
            Is.True);
        FlowchartDocxEmbedMetrics.EmbedReport report = FlowchartDocxEmbedMetrics.Measure(layout);
        double fanWidth = FanSpan(layout, 2, 31);
        double tailWidth = FanSpan(layout, 32, 160);
        TestContext.Out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"WORD_SCALE|F153|scale={report.WidthScale:0.###}|px={report.WidthPixels:0}x{report.HeightPixels:0}|fanPx={fanWidth:0}|tailPx={tailWidth:0}"));
        Assert.That(report.WidthPixels, Is.LessThanOrEqualTo(FlowchartLayoutRules.DiagramUsableWidthPx + 1.0));
        Assert.That(report.LabelReadable, Is.True);
    }

    private static string ReadScenario(string id)
    {
        string markdown = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "flow.md"));
        Match section = Regex.Match(markdown, $@"(?ms)^### {id}\b(?<body>.*?)(?=^### |\z)");
        Match fence = Regex.Match(section.Groups["body"].Value, @"(?ms)```mermaid\s*\r?\n(?<source>.*?)\r?\n```");
        return fence.Groups["source"].Value;
    }

    private static double FanSpan(FlowchartLayoutResult layout, int from, int to)
    {
        var boxes = layout.NodeBounds
            .Where(box => int.TryParse(box.NodeId.AsSpan(1), out int id) && id >= from && id <= to)
            .ToArray();
        if (boxes.Length == 0) return 0;
        double left = boxes.Min(box => box.CenterX - box.Width / 2.0);
        double right = boxes.Max(box => box.CenterX + box.Width / 2.0);
        return right - left;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor != null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "flow.md")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
