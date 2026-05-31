using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Layout;
using MermaidSharp.Models;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class TbTwoByTwoCompleteBipartiteRenderTests
{
    const string GROUPED_TWO_BY_TWO_SOURCE = """
        flowchart TB
        A & B --> C & D
        """;

    [Test]
    public void TryRenderFlowchartSvg_GroupedTwoByTwo_ProducesFourDistinctConnectors()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            GROUPED_TWO_BY_TWO_SOURCE,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        AssertConnectorGeometry(Svg);
    }

    [Test]
    public void LegacyParser_Render_GroupedTwoByTwo_ProducesFourDistinctConnectors()
    {
        FlowchartParser Parser = new FlowchartParser();
        var ParseOutcome = Parser.Parse(GROUPED_TWO_BY_TWO_SOURCE);
        Assert.That(ParseOutcome.Success, Is.True);
        Assert.That(ParseOutcome.Value.Edges.Count, Is.EqualTo(4));

        FlowchartRenderer Renderer = new FlowchartRenderer();
        SvgDocument Document = Renderer.Render(ParseOutcome.Value, RenderOptions.Default);
        StringBuilder Builder = new StringBuilder();
        Document.ToXml(Builder);
        string Svg = Builder.ToString();

        AssertConnectorGeometry(Svg);
    }

    [Test]
    public void Render_F61_AfterLayout_CrossEdges_UseTwoPointStraightRoutes()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(GROUPED_TWO_BY_TWO_SOURCE));
        FlowchartRenderer Renderer = new FlowchartRenderer();
        Renderer.Render(Model, RenderOptions.Default);

        Edge AToD = Model.Edges.Single(Edge =>
            string.Equals(Edge.SourceId, "A", StringComparison.Ordinal)
            && string.Equals(Edge.TargetId, "D", StringComparison.Ordinal));
        Edge BToC = Model.Edges.Single(Edge =>
            string.Equals(Edge.SourceId, "B", StringComparison.Ordinal)
            && string.Equals(Edge.TargetId, "C", StringComparison.Ordinal));

        Assert.That(AToD.Points.Count, Is.EqualTo(2));
        Assert.That(BToC.Points.Count, Is.EqualTo(2));
    }

    [Test]
    public void Layout_F61_GroupedTwoByTwo_UsesCentreVerticalAndCornerCrossRoutes()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(GROUPED_TWO_BY_TWO_SOURCE));
        new DagreLayoutEngine().Layout(Model, LayoutOptions.Default);

        Node A = Model.GetNode("A")!;
        Node B = Model.GetNode("B")!;
        Node C = Model.GetNode("C")!;
        Node D = Model.GetNode("D")!;

        AssertVerticalColumnEdge(Model, A, C);
        AssertVerticalColumnEdge(Model, B, D);
        AssertCrossCornerEdge(Model, A, D, expectSourceSouthEast: true, expectTargetNorthWest: true);
        AssertCrossCornerEdge(Model, B, C, expectSourceSouthWest: true, expectTargetNorthEast: true);
    }

    static void AssertConnectorGeometry(string Svg)
    {
        MatchCollection LinkPaths = Regex.Matches(Svg, @"<path[^>]*class=""flowchart-link""[^>]*\s+d=""([^""]+)""", RegexOptions.IgnoreCase);
        if (LinkPaths.Count == 0)
        {
            LinkPaths = Regex.Matches(Svg, @"d=""([^""]+)""[^>]*class=""flowchart-link""", RegexOptions.IgnoreCase);
        }

        Assert.That(LinkPaths.Count, Is.EqualTo(4), "Expected four edge paths for 2×2 grouped expansion.");

        int VerticalColumnCount = 0;
        int StraightDiagonalCount = 0;
        for (int PathIndex = 0; PathIndex < LinkPaths.Count; PathIndex++)
        {
            string PathData = LinkPaths[PathIndex].Groups[1].Value;
            if (IsMostlyVerticalColumn(PathData))
            {
                VerticalColumnCount++;
            }
            else if (IsStraightDiagonal(PathData))
            {
                StraightDiagonalCount++;
            }
        }

        Assert.That(VerticalColumnCount, Is.EqualTo(2), "Same-column pairs should render as vertical drops.");
        Assert.That(StraightDiagonalCount, Is.EqualTo(2), "Cross-column pairs should render as straight diagonals.");
    }

    static void AssertVerticalColumnEdge(FlowchartModel Model, Node Source, Node Target)
    {
        Edge Edge = Model.Edges.Single(Candidate =>
            string.Equals(Candidate.SourceId, Source.Id, StringComparison.Ordinal)
            && string.Equals(Candidate.TargetId, Target.Id, StringComparison.Ordinal));
        Assert.That(Edge.Points.Count, Is.GreaterThanOrEqualTo(2));

        Position Start = Edge.Points[0];
        Position End = Edge.Points[^1];
        Assert.That(Math.Abs(Start.X - Source.Position.X), Is.LessThan(2.0),
            () => $"{Source.Id}->{Target.Id} should leave the bottom-centre of the source");
        Assert.That(Math.Abs(End.X - Target.Position.X), Is.LessThan(2.0),
            () => $"{Source.Id}->{Target.Id} should enter the top-centre of the target");
        Assert.That(Math.Abs(Start.X - End.X), Is.LessThan(2.0),
            () => $"{Source.Id}->{Target.Id} should run in a vertical column");
    }

    static void AssertCrossCornerEdge(
        FlowchartModel Model,
        Node Source,
        Node Target,
        bool expectSourceSouthEast = false,
        bool expectSourceSouthWest = false,
        bool expectTargetNorthWest = false,
        bool expectTargetNorthEast = false)
    {
        Edge Edge = Model.Edges.Single(Candidate =>
            string.Equals(Candidate.SourceId, Source.Id, StringComparison.Ordinal)
            && string.Equals(Candidate.TargetId, Target.Id, StringComparison.Ordinal));
        Assert.That(Edge.Points.Count, Is.GreaterThanOrEqualTo(2));

        double SourceLeft = Source.Position.X - (Source.Width / 2.0);
        double SourceRight = Source.Position.X + (Source.Width / 2.0);
        double TargetLeft = Target.Position.X - (Target.Width / 2.0);
        double TargetRight = Target.Position.X + (Target.Width / 2.0);
        Position Start = Edge.Points[0];
        Position End = Edge.Points[^1];

        if (expectSourceSouthEast)
        {
            Assert.That(Math.Abs(Start.X - SourceRight), Is.LessThan(2.0));
        }

        if (expectSourceSouthWest)
        {
            Assert.That(Math.Abs(Start.X - SourceLeft), Is.LessThan(2.0));
        }

        if (expectTargetNorthWest)
        {
            Assert.That(Math.Abs(End.X - TargetLeft), Is.LessThan(2.0));
        }

        if (expectTargetNorthEast)
        {
            Assert.That(Math.Abs(End.X - TargetRight), Is.LessThan(2.0));
        }

        Assert.That(Edge.Points.Count, Is.EqualTo(2),
            () => $"{Source.Id}->{Target.Id} crossover should be a straight segment");
        Assert.That(Math.Abs(Start.X - End.X), Is.GreaterThan(4.0));
        Assert.That(Math.Abs(Start.Y - End.Y), Is.GreaterThan(4.0));
    }

    static bool IsStraightDiagonal(string PathData)
    {
        if (PathData.Contains('C', StringComparison.Ordinal))
        {
            return false;
        }

        Match StartMatch = Regex.Match(PathData, @"^M\s*([-\d.]+)\s*,\s*([-\d.]+)");
        Match EndMatch = Regex.Match(PathData, @"([-\d.]+)\s*,\s*([-\d.]+)\s*$");
        if (!StartMatch.Success || !EndMatch.Success)
        {
            return false;
        }

        double StartX = double.Parse(StartMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double StartY = double.Parse(StartMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double EndX = double.Parse(EndMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double EndY = double.Parse(EndMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        return Math.Abs(StartX - EndX) > 4.0 && Math.Abs(StartY - EndY) > 4.0;
    }

    static bool IsMostlyVerticalColumn(string PathData)
    {
        Match StartMatch = Regex.Match(PathData, @"^M\s*([-\d.]+)\s*,\s*([-\d.]+)");
        Match EndMatch = Regex.Match(PathData, @"([-\d.]+)\s*,\s*([-\d.]+)\s*$");
        if (!StartMatch.Success || !EndMatch.Success)
        {
            return false;
        }

        double StartX = double.Parse(StartMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double EndX = double.Parse(EndMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        return Math.Abs(StartX - EndX) < 1.5;
    }
}
