using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartSugiyamaLayoutEngineTests
{
    [Test]
    public void TryLayout_Chain_Fixture_Produces_Separated_Node_Centres()
    {
        string MmdPath = Path.Combine(FindFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);
        Assert.That(Result.NodeBounds.Count, Is.EqualTo(Contract.Nodes.Count));
        Assert.That(Result.GraphWidth, Is.GreaterThan(0));
        Assert.That(Result.GraphHeight, Is.GreaterThan(0));

        List<double> CentreYValues = Result.NodeBounds.Select(Bounds => Bounds.CenterY).Distinct().ToList();
        Assert.That(CentreYValues.Count, Is.GreaterThan(1), "Sugiyama should spread chain nodes across layers");
    }

    [Test]
    public void TryLayout_Decision_FanOut_Fixture_Produces_Bounds_For_All_Nodes()
    {
        string Source = """
            flowchart TD
                Choose{Choose}
                Choose -->|Yes| Approve[Approved]
                Choose -->|No| Reject[Rejected]
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);
        Assert.That(Result.NodeBounds.Count, Is.EqualTo(3));
    }

    [Test]
    public void TryLayout_CustomRankSpacing_IncreasesVerticalSpan()
    {
        string Source = """
            flowchart TD
            A --> B --> C
            """;
        DiagramContract DefaultContract = MermaidRecognizer.Parse(Source);
        DiagramContract WideContract = MermaidRecognizer.Parse("""
            %%{init: {"flowchart": {"rankSpacing": 120}}}%%
            flowchart TD
            A --> B --> C
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(DefaultContract, out FlowchartLayoutResult DefaultResult), Is.True);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(WideContract, out FlowchartLayoutResult WideResult), Is.True);

        double DefaultSpan = DefaultResult.NodeBounds.Max(Bounds => Bounds.CenterY)
            - DefaultResult.NodeBounds.Min(Bounds => Bounds.CenterY);
        double WideSpan = WideResult.NodeBounds.Max(Bounds => Bounds.CenterY)
            - WideResult.NodeBounds.Min(Bounds => Bounds.CenterY);

        Assert.That(WideSpan, Is.GreaterThan(DefaultSpan));
    }

    [Test]
    public void TryLayout_F10_LR_Chain_MaintainsRankSpacingGap()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["Label with spaces"] --> B["Second label"]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);

        FlowchartLayoutNodeBounds NodeA = Result.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds NodeB = Result.NodeBounds.First(Bounds => Bounds.NodeId == "B");

        Assert.That(NodeA.CenterX, Is.LessThan(NodeB.CenterX));

        double RightA = NodeA.CenterX + (NodeA.Width / 2.0);
        double LeftB = NodeB.CenterX - (NodeB.Width / 2.0);
        double Gap = LeftB - RightA;

        Assert.That(Gap, Is.GreaterThanOrEqualTo(49.0));
    }

    [Test]
    public void TryLayout_F10_QuotedLabels_LR_NodesAreWiderThanTall()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["Label with spaces"] --> B["Second label"]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);

        foreach (FlowchartLayoutNodeBounds Bounds in Result.NodeBounds)
        {
            Assert.That(
                Bounds.Width,
                Is.GreaterThan(Bounds.Height),
                $"Node {Bounds.NodeId} should be wider than tall for horizontal LR labels");
        }

        FlowchartNodeDimensions NodeA = FlowchartNodeSizeEstimator.Estimate(
            Contract.Nodes.First(Node => Node.Id == "A"),
            FlowchartRenderOptions.FromContract(Contract));

        FlowchartLayoutNodeBounds LayoutA = Result.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        Assert.That(LayoutA.Width, Is.GreaterThanOrEqualTo(NodeA.Width * 0.9));
    }

    [Test]
    public void TryLayout_F02_LR_Chain_StartIsLeftOfEnd()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            Start[Start] --> Middle[Middle] --> End[End]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);

        double StartX = Result.NodeBounds.First(Bounds => Bounds.NodeId == "Start").CenterX;
        double EndX = Result.NodeBounds.First(Bounds => Bounds.NodeId == "End").CenterX;

        Assert.That(StartX, Is.LessThan(EndX), "LR chain should place Start left of End");
    }

    [Test]
    public void TryLayout_F06_RL_Chain_EastIsRightOfWest()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart RL
            East[East] --> Centre[Centre] --> West[West]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);

        double EastX = Result.NodeBounds.First(Bounds => Bounds.NodeId == "East").CenterX;
        double WestX = Result.NodeBounds.First(Bounds => Bounds.NodeId == "West").CenterX;

        Assert.That(EastX, Is.GreaterThan(WestX), "RL chain should place East right of West");
    }

    [Test]
    public void TryLayout_LR_Chain_SpreadsHorizontally()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A --> B --> C
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);

        double XSpan = Result.NodeBounds.Max(Bounds => Bounds.CenterX) - Result.NodeBounds.Min(Bounds => Bounds.CenterX);
        double YSpan = Result.NodeBounds.Max(Bounds => Bounds.CenterY) - Result.NodeBounds.Min(Bounds => Bounds.CenterY);

        Assert.That(XSpan, Is.GreaterThan(YSpan));
    }

    [Test]
    public void TryLayout_DefaultDirection_Chain_SpreadsVertically()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart
            A --> B --> C
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.True);

        double XSpan = Result.NodeBounds.Max(Bounds => Bounds.CenterX) - Result.NodeBounds.Min(Bounds => Bounds.CenterX);
        double YSpan = Result.NodeBounds.Max(Bounds => Bounds.CenterY) - Result.NodeBounds.Min(Bounds => Bounds.CenterY);

        Assert.That(YSpan, Is.GreaterThan(XSpan));
    }

    [Test]
    public void TryLayout_Empty_Contract_Returns_False()
    {
        DiagramContract Contract = new DiagramContract();
        Contract.Kind = "flowchart";

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Result), Is.False);
    }

    private static string FindFlowchartFixtureDirectory()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, "FlowchartRecognition");
            if (Directory.Exists(Candidate))
            {
                return Candidate;
            }

            Current = Current.Parent;
        }

        throw new InvalidOperationException("Could not locate FlowchartRecognition fixtures.");
    }
}
