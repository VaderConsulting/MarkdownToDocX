using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CompactLayoutMetricTests
{
    [TestCase("F", 151)]
    [TestCase("F", 152)]
    [TestCase("F", 153)]
    [TestCase("A", 142)]
    public void CatalogueDiagram_ReportsCompactLayoutMetrics(string Catalogue, int Number)
    {
        string Source = ReadScenario(Catalogue, Number);
        DiagramContract Contract = Catalogue == "A"
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document)
            : MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        (double Width, double Height) = Bounds(Layout);
        var Violations = new List<FlowchartLayoutQualityFailure>();
        DiagramGridQuality.Collect(Contract, Layout, Violations);
        int Overlaps = Violations.Count(Failure => Failure.Kind == "NodeOverlap");
        int Crossings = CountCrossings(Layout);
        double EmptyShare = EmptyAreaShare(Layout, Width, Height);
        TestContext.Out.WriteLine(
            $"{Catalogue}{Number}: width={Width:0} height={Height:0} ratio={Width / Height:0.000} overlaps={Overlaps} crossings={Crossings} empty={EmptyShare:P1}");
    }

    private static (double Width, double Height) Bounds(FlowchartLayoutResult Layout)
    {
        var X = new List<double>();
        var Y = new List<double>();
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
        {
            X.Add(Box.CenterX - Box.Width / 2.0); X.Add(Box.CenterX + Box.Width / 2.0);
            Y.Add(Box.CenterY - Box.Height / 2.0); Y.Add(Box.CenterY + Box.Height / 2.0);
        }
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            X.Add(Point.X); Y.Add(Point.Y);
        }
        return (X.Max() - X.Min(), Y.Max() - Y.Min());
    }

    private static int CountCrossings(FlowchartLayoutResult Layout)
    {
        int Result = 0;
        for (int Left = 0; Left < Layout.EdgeRoutes.Count; Left++)
        for (int Right = Left + 1; Right < Layout.EdgeRoutes.Count; Right++)
        {
            FlowchartLayoutEdgeRoute A = Layout.EdgeRoutes[Left];
            FlowchartLayoutEdgeRoute B = Layout.EdgeRoutes[Right];
            if (A.SourceId == B.SourceId || A.SourceId == B.TargetId || A.TargetId == B.SourceId || A.TargetId == B.TargetId) continue;
            Result += FlowchartBranchRouteOrder.Intersections(
                A.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                B.Points.Select(Point => (Point.X, Point.Y)).ToList()).Crossings;
        }
        return Result;
    }

    private static double EmptyAreaShare(FlowchartLayoutResult Layout, double Width, double Height)
    {
        const double Cell = DiagramGridMetrics.GRID_UNIT_PX;
        double Left = Layout.NodeBounds.Min(Box => Box.CenterX - Box.Width / 2.0);
        double Bottom = Layout.NodeBounds.Min(Box => Box.CenterY - Box.Height / 2.0);
        int Columns = Math.Max(1, (int)Math.Ceiling(Width / Cell));
        int Rows = Math.Max(1, (int)Math.Ceiling(Height / Cell));
        var Used = new HashSet<(int Column, int Row)>();
        void Mark(double MinX, double MinY, double MaxX, double MaxY)
        {
            int FirstColumn = (int)Math.Floor((MinX - Left) / Cell);
            int LastColumn = (int)Math.Floor((MaxX - Left) / Cell);
            int FirstRow = (int)Math.Floor((MinY - Bottom) / Cell);
            int LastRow = (int)Math.Floor((MaxY - Bottom) / Cell);
            for (int Column = FirstColumn; Column <= LastColumn; Column++)
            for (int Row = FirstRow; Row <= LastRow; Row++)
                if (Column >= 0 && Column < Columns && Row >= 0 && Row < Rows) Used.Add((Column, Row));
        }
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds)
            Mark(Box.CenterX - Box.Width / 2.0, Box.CenterY - Box.Height / 2.0,
                Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0);
        foreach (FlowchartLayoutNodeBounds Box in Layout.ClusterBounds)
            Mark(Box.CenterX - Box.Width / 2.0, Box.CenterY - Box.Height / 2.0,
                Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Route.Points[Index - 1], B = Route.Points[Index];
            Mark(Math.Min(A.X, B.X) - Cell / 2.0, Math.Min(A.Y, B.Y) - Cell / 2.0,
                Math.Max(A.X, B.X) + Cell / 2.0, Math.Max(A.Y, B.Y) + Cell / 2.0);
        }
        return 1.0 - Used.Count / (double)(Columns * Rows);
    }

    private static string ReadScenario(string Catalogue, int Number)
    {
        string Markdown = File.ReadAllText(FindRepositoryFile(Catalogue == "F" ? "flow.md" : "enterprise.md"));
        Match Section = Regex.Match(Markdown, $@"(?ms)^### {Catalogue}{Number}\b(?<body>.*?)(?=^### {Catalogue}\d+\b|\z)");
        Assert.That(Section.Success, Is.True);
        Match Fence = Regex.Match(Section.Groups["body"].Value, @"(?ms)\x60\x60\x60mermaid\s*\r?\n(?<source>.*?)\r?\n\x60\x60\x60");
        Assert.That(Fence.Success, Is.True);
        return Fence.Groups["source"].Value;
    }

    private static string FindRepositoryFile(string FileName)
    {
        DirectoryInfo? Cursor = new(AppContext.BaseDirectory);
        while (Cursor != null)
        {
            string Candidate = Path.Combine(Cursor.FullName, FileName);
            if (File.Exists(Candidate)) return Candidate;
            Cursor = Cursor.Parent;
        }
        throw new FileNotFoundException(FileName);
    }
}
