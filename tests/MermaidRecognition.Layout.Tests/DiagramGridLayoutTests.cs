using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Grid sizes, straight runs, and group frames that keep their titles visible.
/// </summary>
[TestFixture]
public sealed class DiagramGridLayoutTests
{
    [Test]
    public void EqualCentres_StandardBox_UsesTheFiveSlotPattern()
    {
        // Centres are line_spacing (15 px) apart and centred on the side. The 150 px width
        // spans 45–105; the 60 px height is too short for five 15 px slots, so the span is
        // pinned to the edges at 0–60 instead of the old 12 px even division.
        int[] Horizontal = DiagramGridMetrics.EqualCentres(DiagramGridMetrics.NODE_WIDTH_PX, DiagramGridMetrics.TRACKS_PER_GAP);
        int[] Vertical = DiagramGridMetrics.EqualCentres(DiagramGridMetrics.NODE_HEIGHT_PX, DiagramGridMetrics.TRACKS_PER_GAP);

        Assert.That(Horizontal, Is.EqualTo(new[] { 45, 60, 75, 90, 105 }));
        Assert.That(Vertical, Is.EqualTo(new[] { 0, 15, 30, 45, 60 }));
    }

    [Test]
    public void LayoutStages_RunPlacementHintsBeforeAutomaticPlacementAndRouting()
    {
        DiagramContract Contract = Parse("""
            flowchart TB
            A[Start] --> B[Finish]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Assert.That(Layout.RenderContext.LayoutStageTrace, Is.EqualTo(new[]
        {
            "grid-and-measured-geometry",
            "placement-hints-2.21",
            "automatic-node-placement-2.x",
            "group-frame-fitting",
            "route-coordinate-preparation",
            "joint-port-allocation-4.7",
            "line-routing-hints-3.1-3.7",
            "rule-specific-route-construction",
            "coupled-route-repair",
            "final-legality-validation-section-10"
        }));
    }

    [Test]
    public void TrackOffsets_FollowTheCentreOutwardPattern()
    {
        Assert.That(DiagramGridMetrics.TrackOffsets(1, DiagramGridMetrics.GRID_UNIT_PX), Is.EqualTo(new[] { 15 }));
        Assert.That(DiagramGridMetrics.TrackOffsets(2, DiagramGridMetrics.GRID_UNIT_PX), Is.EqualTo(new[] { 15, 30 }));
        Assert.That(DiagramGridMetrics.TrackOffsets(3, DiagramGridMetrics.GRID_UNIT_PX), Is.EqualTo(new[] { 15, 30, 45 }));
        Assert.That(DiagramGridMetrics.TrackOffsets(5, DiagramGridMetrics.GRID_UNIT_PX), Is.EqualTo(new[] { 15, 30, 45, 60, 75 }));
    }

    [Test]
    public void ResolveStandardBox_ShortLabels_StayTheStandardSize()
    {
        DiagramContract Contract = Parse("""
            flowchart TB
            A[In] --> B[Out]
            """);

        (int WidthPx, int HeightPx) = DiagramGridLayout.ResolveStandardBox(Contract);

        Assert.That(WidthPx, Is.EqualTo(DiagramGridMetrics.NODE_WIDTH_PX));
        Assert.That(HeightPx, Is.EqualTo(DiagramGridMetrics.NODE_HEIGHT_PX));
    }

    [Test]
    public void ResolveStandardBox_LongLabel_GrowsEveryNodeInWholeUnits()
    {
        DiagramContract Contract = Parse("""
            flowchart LR
            A[In] --> B[Publish a manifest file list sizes hashes with each drop so the consumer can reject incomplete batches of uploaded documents]
            """);

        (int WidthPx, int HeightPx) = DiagramGridLayout.ResolveStandardBox(Contract);

        Assert.That(WidthPx, Is.GreaterThan(DiagramGridMetrics.NODE_WIDTH_PX));
        Assert.That(WidthPx % DiagramGridMetrics.GRID_UNIT_PX, Is.EqualTo(0));
        Assert.That(HeightPx % DiagramGridMetrics.GRID_UNIT_PX, Is.EqualTo(0));
    }

    [Test]
    public void TryLayout_Decision_IsStandardWidthAndTwiceTheHeight()
    {
        DiagramContract Contract = Parse("""
            flowchart TB
            Start[Start] --> Check{Valid?}
            Check --> Done[Done]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
            new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult Layout), Is.True);
        FlowchartCellRegion Check = Layout.CellNodes!.Single(Node => Node.Id == "Check").CellNode!.Region;
        Assert.That(Check.ColumnSpan, Is.EqualTo(1));
        Assert.That(Check.RowSpan, Is.EqualTo(2));
    }

    [Test]
    public void TryLayout_AlignedChain_DrawsAStraightRun()
    {
        DiagramContract Contract = Parse("""
            flowchart TB
            A[In] --> B[Middle] --> C[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutEdgeRoute First = FindRoute(Layout, "A", "B");

        Assert.That(First.Points, Has.Count.EqualTo(2));
        Assert.That(First.Points[0].X, Is.EqualTo(First.Points[1].X).Within(0.1));
        Assert.That(Find(Layout, "A").CenterX, Is.EqualTo(Find(Layout, "B").CenterX).Within(0.1));
        Assert.That(Find(Layout, "B").CenterX, Is.EqualTo(Find(Layout, "C").CenterX).Within(0.1));
    }

    [Test]
    public void TryLayout_GroupFrame_KeepsTheTitleClearOfTheBox()
    {
        DiagramContract Contract = Parse("""
            flowchart LR
                subgraph R0S0["Manifests and checksums"]
                    direction TB
                    r0n0["Publish a manifest (file list, sizes, hashes) with each drop so the consumer can reject incomplete batches"]
                end
                IN[In] --> r0n0
                r0n0 --> OUT[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutNodeBounds Node = Find(Layout, "r0n0");
        FlowchartLayoutNodeBounds Frame = FindCluster(Layout, "R0S0");
        double FrameTop = Frame.CenterY + (Frame.Height / 2.0);
        double FrameBottom = Frame.CenterY - (Frame.Height / 2.0);
        double FrameLeft = Frame.CenterX - (Frame.Width / 2.0);
        double FrameRight = Frame.CenterX + (Frame.Width / 2.0);
        double NodeTop = Node.CenterY + (Node.Height / 2.0);
        double NodeBottom = Node.CenterY - (Node.Height / 2.0);
        double NodeLeft = Node.CenterX - (Node.Width / 2.0);
        double NodeRight = Node.CenterX + (Node.Width / 2.0);

        double LeftPad = NodeLeft - FrameLeft;
        double RightPad = FrameRight - NodeRight;
        Assert.That(FrameTop - NodeTop, Is.GreaterThanOrEqualTo(DiagramGridMetrics.NODE_GAP_PX + DiagramGridMetrics.GRID_UNIT_PX - 0.5));
        Assert.That(NodeBottom - FrameBottom, Is.EqualTo(DiagramGridMetrics.NODE_GAP_PX).Within(1.5));
        Assert.That(LeftPad, Is.GreaterThanOrEqualTo(DiagramGridMetrics.NODE_GAP_PX - 0.5));
        Assert.That(RightPad, Is.EqualTo(LeftPad).Within(1.5));
        Assert.That(Frame.Width, Is.GreaterThanOrEqualTo(Node.Width + (2 * DiagramGridMetrics.NODE_GAP_PX) - 0.5));
        double TitleWidth = FlowchartRichLabelSvgWriter.EstimateLineWidthPx("Manifests and checksums", 16.0);
        Assert.That(Frame.Width, Is.GreaterThanOrEqualTo(TitleWidth));
    }

    [Test]
    public void TryLayout_NestedGroup_SitsInsideTheOuterPaddingBelowTheOuterTitle()
    {
        DiagramContract Contract = Parse("""
            flowchart TB
                subgraph Outer["Outer system"]
                    subgraph Inner["Inner system"]
                        A[Work]
                    end
                end
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutNodeBounds Outer = FindCluster(Layout, "Outer");
        FlowchartLayoutNodeBounds Inner = FindCluster(Layout, "Inner");
        double OuterTop = Outer.CenterY + (Outer.Height / 2.0);
        double OuterBottom = Outer.CenterY - (Outer.Height / 2.0);
        double OuterLeft = Outer.CenterX - (Outer.Width / 2.0);
        double OuterRight = Outer.CenterX + (Outer.Width / 2.0);
        double InnerTop = Inner.CenterY + (Inner.Height / 2.0);
        double InnerBottom = Inner.CenterY - (Inner.Height / 2.0);
        double InnerLeft = Inner.CenterX - (Inner.Width / 2.0);
        double InnerRight = Inner.CenterX + (Inner.Width / 2.0);

        Assert.That(OuterTop - InnerTop, Is.GreaterThanOrEqualTo(DiagramGridMetrics.NODE_GAP_PX + DiagramGridMetrics.GRID_UNIT_PX - 0.5));
        Assert.That(InnerBottom - OuterBottom, Is.GreaterThanOrEqualTo(DiagramGridMetrics.NODE_GAP_PX - 0.5));
        Assert.That(InnerLeft - OuterLeft, Is.GreaterThanOrEqualTo(DiagramGridMetrics.NODE_GAP_PX - 0.5));
        Assert.That(OuterRight - InnerRight, Is.GreaterThanOrEqualTo(DiagramGridMetrics.NODE_GAP_PX - 0.5));
    }

    private static DiagramContract Parse(string Source)
    {
        return MermaidRecognizer.Parse(Source);
    }

    private static FlowchartLayoutNodeBounds Find(FlowchartLayoutResult Layout, string Id)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            if (string.Equals(Bounds.NodeId, Id, StringComparison.Ordinal))
            {
                return Bounds;
            }
        }

        Assert.Fail("Missing node " + Id);
        return new FlowchartLayoutNodeBounds();
    }

    private static FlowchartLayoutNodeBounds FindCluster(FlowchartLayoutResult Layout, string Id)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.ClusterBounds)
        {
            if (string.Equals(Bounds.NodeId, Id, StringComparison.Ordinal))
            {
                return Bounds;
            }
        }

        Assert.Fail("Missing group " + Id);
        return new FlowchartLayoutNodeBounds();
    }

    private static FlowchartLayoutEdgeRoute FindRoute(FlowchartLayoutResult Layout, string SourceId, string TargetId)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (string.Equals(Route.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Route.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Route;
            }
        }

        Assert.Fail("Missing route " + SourceId + "->" + TargetId);
        return new FlowchartLayoutEdgeRoute();
    }
}
