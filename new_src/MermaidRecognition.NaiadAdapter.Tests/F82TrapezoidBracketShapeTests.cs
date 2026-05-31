using MermaidRecognition.NaiadAdapter;
using MermaidSharp;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F82TrapezoidBracketShapeTests
{
    const string F82_FOUR_SOURCE = """
        flowchart LR
        LeanRight[/lean_right/]
        Trapezoid[/trapezoid \]
        InvTrap[\inv_trapezoid /]
        LeanLeft[\lean_left \]
        """;

    [Test]
    public void TryRenderFlowchartSvg_TrapezoidBracket_IsWideAtBottom()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            F82_FOUR_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.That(Rendered, Is.True);

        Dictionary<string, string> PathsByLabel = ExtractNodePathsByLabel(Svg);
        Assert.That(PathsByLabel["trapezoid"], Is.Not.Null);
        Assert.That(PathsByLabel["inv_trapezoid"], Is.Not.Null);

        Assert.That(
            GetHorizontalSpanAtBottomEdge(PathsByLabel["trapezoid"]),
            Is.GreaterThan(GetHorizontalSpanAtTopEdge(PathsByLabel["trapezoid"])),
            "[/trapezoid \\] must be wider at the bottom");
        Assert.That(
            GetHorizontalSpanAtTopEdge(PathsByLabel["inv_trapezoid"]),
            Is.GreaterThan(GetHorizontalSpanAtBottomEdge(PathsByLabel["inv_trapezoid"])),
            "[\\inv_trapezoid /] must be wider at the top");
    }

    static Dictionary<string, string> ExtractNodePathsByLabel(string Svg)
    {
        Dictionary<string, string> Result = new Dictionary<string, string>(StringComparer.Ordinal);
        System.Text.RegularExpressions.MatchCollection PathMatches = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path\b[^>]*\bd=""([^""]+)""[^>]*\bclass=""node""");
        System.Text.RegularExpressions.MatchCollection LabelMatches = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"class=""nodeLabel""[^>]*>[\s\S]*?<tspan[^>]*>([^<]+)</tspan>");

        int Count = Math.Min(PathMatches.Count, LabelMatches.Count);
        for (int Index = 0; Index < Count; Index++)
        {
            Result[LabelMatches[Index].Groups[1].Value.Trim()] = PathMatches[Index].Groups[1].Value;
        }

        return Result;
    }

    static double GetHorizontalSpanAtTopEdge(string PathData)
    {
        return GetHorizontalSpanAtExtremeY(PathData, UseMaxY: false);
    }

    static double GetHorizontalSpanAtBottomEdge(string PathData)
    {
        return GetHorizontalSpanAtExtremeY(PathData, UseMaxY: true);
    }

    static double GetHorizontalSpanAtExtremeY(string PathData, bool UseMaxY)
    {
        List<(double X, double Y)> Points = new List<(double X, double Y)>();
        foreach (System.Text.RegularExpressions.Match Match in System.Text.RegularExpressions.Regex.Matches(
                     PathData,
                     @"([ML])\s*([-\d.]+)\s*,\s*([-\d.]+)"))
        {
            Points.Add((double.Parse(Match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(Match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        double TargetY = UseMaxY
            ? Points.Max(Point => Point.Y)
            : Points.Min(Point => Point.Y);
        List<double> XValues = Points.Where(Point => Math.Abs(Point.Y - TargetY) < 0.01).Select(Point => Point.X).ToList();
        return XValues.Max() - XValues.Min();
    }
}
