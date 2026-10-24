using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

/// <summary>Checks the centre-out connection-point grid of rules 3.2 and 4.7.</summary>
[TestFixture, NonParallelizable]
public sealed class CentreOutPortGridTests
{
    private const double SPACING = 15;

    private bool PreviousCells;

    /// <summary>Lays out with production cell ownership, as the renderer does outside the test host.</summary>
    [SetUp]
    public void UseProductionCells()
    {
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out PreviousCells);
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
    }

    /// <summary>Restores the test host's compatibility default.</summary>
    [TearDown]
    public void RestoreCellSwitch()
    {
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, PreviousCells);
    }

    private const string COMPLETE_SAMPLE = """
        enterprise
          title All options
          version 3.2
          direction TB
          accTitle: All options
          accDescr: Every enterprise clause
          classDef emphasis fill:#fff3cd,stroke:#333333
          group shop "Shop" {
            direction LR
            application-component webShop "Web Shop":::emphasis
          }
          grouping box "Box" in shop
          application-component part "Part" in shop
          application-service orderApi "Order API" in box
          data-object order "Order" below orderApi
          data-object ledger "Ledger" above order +1
          business-actor customer "Customer" left-of webShop
          application-component neighbour "Neighbour" right-of webShop +1
          business-process checkout "Checkout" push E
          business-process review "Review" push W
          business-process delay "Delay" push N
          business-process cancel "Cancel" push S
          application-interface contract "Contract"
          application-service baseApi "Base API"
          application-component shared "Shared"
          data-object audit "Audit"
          business-process rating "Rating"
          application-component partner "Partner"
          goal ship "Ship on time"
          outcome late "Late orders"
          driver cost "Cost"
          assessment risk "Risk"
          requirement mustStore "Must store"
          note margin "A note beside the order" below order +2
          and-junction split "Split" above checkout
          or-junction choice "Choice" below checkout +1
          style order fill:#eef
          class webShop emphasis
          customer -assignment-> checkout "Assigned" [leave E, travel E, enter W]
          webShop -composition-> part
          webShop -aggregation-> shared
          webShop -realization-> contract
          webShop -serving-> orderApi "Serves"
          orderApi -access(read)-> order
          orderApi -access(write)-> ledger
          orderApi -access(readwrite)-> audit
          orderApi -access(access)-> order
          customer -influence(+)-> ship
          customer -influence(++)-> late
          customer -influence(-)-> cost
          customer -influence(--)-> risk
          customer -influence(2)-> mustStore
          webShop -triggering-> checkout "Starts"
          orderApi -flow-> checkout "Moves"
          orderApi -specialization-> baseApi
          webShop -association-> neighbour
          webShop -association(directed)-> partner "Directed"
          margin --- order
          linkStyle 0 stroke:#333333
        """;

    [Test]
    public void AssignedLine_MovesOntoASharedRowAndRunsStraight()
    {
        (EnterpriseLayoutResult Layout, List<EnterpriseRoutedEdge> Routes) = LayOut(COMPLETE_SAMPLE);
        EnterpriseRoutedEdge Route = Routes.Single(Edge => Edge.Relationship.SourceId == "customer" && Edge.Relationship.TargetId == "checkout");
        EnterpriseLayoutNode Customer = Layout.Nodes["customer"];
        EnterpriseLayoutNode Checkout = Layout.Nodes["checkout"];
        Assert.Multiple(() =>
        {
            Assert.That(Route.Relationship.RoutingHint!.UnhonouredReason, Is.Null);
            Assert.That(Route.SvgPoints, Has.Count.EqualTo(2));
            Assert.That(Route.SvgPoints[0].Y, Is.EqualTo(Route.SvgPoints[1].Y).Within(0.5));
            Assert.That(Route.SvgPoints[1].X, Is.GreaterThan(Route.SvgPoints[0].X));
            Assert.That(Route.SvgPoints[0].X, Is.EqualTo(Customer.AbsoluteX + Customer.Width).Within(0.5));
            Assert.That(Route.SvgPoints[1].X, Is.EqualTo(Checkout.AbsoluteX).Within(0.5));
            Assert.That(Customer.AbsoluteY, Is.LessThan(Checkout.AbsoluteY + Checkout.Height));
            Assert.That(Checkout.AbsoluteY, Is.LessThan(Customer.AbsoluteY + Customer.Height));
        });
    }

    [Test]
    public void DefaultSide_OffersTheCentreAndOneSpacingEitherSide()
    {
        Assert.That(DiagramGridMetrics.SideGridOffsets(60), Is.EqualTo(new[] { 15.0, 30.0, 45.0 }));
    }

    [Test]
    public void LongSide_CountsItsPointsOutFromTheCentre()
    {
        double[] Expected = Enumerable.Range(0, 9).Select(Index => 20.0 + Index * SPACING).ToArray();
        Assert.That(DiagramGridMetrics.SideGridOffsets(160), Is.EqualTo(Expected));
    }

    [TestCase(60)]
    [TestCase(90)]
    [TestCase(160)]
    public void SingleLine_UsesTheSideCentre(double Length)
    {
        Assert.That(DiagramGridMetrics.CentreOutPortOffsets(1, Length), Is.EqualTo(new[] { Length / 2 }));
    }

    [TestCase(60)]
    [TestCase(160)]
    public void ThreeLines_UseTheCentreAndOneSpacingEitherSide(double Length)
    {
        double Centre = Length / 2;
        Assert.That(DiagramGridMetrics.CentreOutPortOffsets(3, Length),
            Is.EqualTo(new[] { Centre - SPACING, Centre, Centre + SPACING }));
    }

    [TestCase(60)]
    [TestCase(160)]
    public void TwoLines_LeaveTheCentreEmpty(double Length)
    {
        double Centre = Length / 2;
        Assert.That(DiagramGridMetrics.CentreOutPortOffsets(2, Length),
            Is.EqualTo(new[] { Centre - SPACING, Centre + SPACING }));
    }

    [TestCase(4, 90)]
    [TestCase(5, 90)]
    [TestCase(6, 120)]
    public void LargerCounts_FillOutwardsOnTheGrid(int Count, double Length)
    {
        double[] Offsets = DiagramGridMetrics.CentreOutPortOffsets(Count, Length);
        double[] Grid = DiagramGridMetrics.SideGridOffsets(Length);
        Assert.That(Offsets, Has.Length.EqualTo(Count));
        Assert.That(Offsets.All(Offset => Grid.Contains(Offset)), Is.True, string.Join(", ", Offsets));
        Assert.That(Offsets.Contains(Length / 2), Is.EqualTo(Count % 2 == 1));
        for (int Index = 0; Index < Count; Index++)
            Assert.That(Offsets[Index] + Offsets[^(Index + 1)], Is.EqualTo(Length).Within(1e-9));
    }

    [TestCase(1, 30)]
    [TestCase(2, 60)]
    [TestCase(3, 60)]
    [TestCase(4, 90)]
    [TestCase(5, 90)]
    public void SideLengthForPorts_HoldsTheCentreOutPattern(int Count, int Expected)
    {
        Assert.That(DiagramGridMetrics.MinSideLengthForPorts(Count), Is.EqualTo(Expected));
        Assert.That(DiagramGridMetrics.CentreOutPortOffsets(Count, Expected), Has.Length.EqualTo(Count));
        Assert.That(DiagramGridMetrics.MaxPortsOnSide(Expected), Is.GreaterThanOrEqualTo(Count));
    }

    [Test]
    public void CompleteSample_CustomerToShipOnTime_IsOneStraightVerticalSegment()
    {
        var (Layout, Routes) = LayOut(COMPLETE_SAMPLE);
        var Route = Routes.Single(Edge => Edge.Relationship.SourceId == "customer" && Edge.Relationship.TargetId == "ship");
        var Customer = Layout.Nodes["customer"];
        var Ship = Layout.Nodes["ship"];
        string Points = $"{Describe(Route.SvgPoints)}; customer {Bounds(Customer)}; ship {Bounds(Ship)}";
        Assert.That(Route.SvgPoints, Has.Count.EqualTo(2), Points);
        Assert.That(Route.SvgPoints[0].X, Is.EqualTo(Route.SvgPoints[1].X).Within(0.5), Points);
        Assert.That(Route.SvgPoints[0].Y, Is.EqualTo(Customer.AbsoluteY).Within(1.0), Points);
        Assert.That(Route.SvgPoints[1].Y, Is.EqualTo(Ship.AbsoluteY + Ship.Height).Within(1.0), Points);
    }

    [Test]
    public void CheckoutFanIn_DoesNotCrossStartsAndMoves()
    {
        string Source = COMPLETE_SAMPLE.Replace("business-process rating \"Rating\"", "business-process rating \"Rating\" left-of delay", StringComparison.Ordinal);
        (EnterpriseLayoutResult Layout, List<EnterpriseRoutedEdge> Routes) = LayOut(Source);
        EnterpriseRoutedEdge Assigned = Routes.Single(Edge => Edge.Relationship.SourceId == "customer" && Edge.Relationship.TargetId == "checkout");
        EnterpriseRoutedEdge Starts = Routes.Single(Edge => Edge.Relationship.SourceId == "webShop" && Edge.Relationship.TargetId == "checkout");
        EnterpriseRoutedEdge Moves = Routes.Single(Edge => Edge.Relationship.SourceId == "orderApi" && Edge.Relationship.TargetId == "checkout");
        EnterpriseLayoutNode Checkout = Layout.Nodes["checkout"];
        Assert.Multiple(() =>
        {
            Assert.That(Assigned.SvgPoints, Has.Count.EqualTo(2), Describe(Assigned.SvgPoints));
            Assert.That(Assigned.SvgPoints[0].Y, Is.EqualTo(Assigned.SvgPoints[1].Y).Within(0.5));
            Assert.That(Starts.SvgPoints[^1].X, Is.EqualTo(Checkout.AbsoluteX).Within(0.5), Describe(Starts.SvgPoints));
            Assert.That(Moves.SvgPoints[^1].X, Is.EqualTo(Checkout.AbsoluteX).Within(0.5), Describe(Moves.SvgPoints));
            Assert.That(Starts.SvgPoints[^1].Y, Is.GreaterThan(Moves.SvgPoints[^1].Y), Describe(Starts.SvgPoints) + " x " + Describe(Moves.SvgPoints));
            Assert.That(SegmentCrossings(Starts.SvgPoints, Moves.SvgPoints), Is.EqualTo(0), Describe(Starts.SvgPoints) + " x " + Describe(Moves.SvgPoints));
            Assert.That(SegmentCrossings(Starts.SvgPoints, Assigned.SvgPoints), Is.EqualTo(0), Describe(Starts.SvgPoints));
            Assert.That(SegmentCrossings(Moves.SvgPoints, Assigned.SvgPoints), Is.EqualTo(0), Describe(Moves.SvgPoints));
        });
    }

    private static int SegmentCrossings(IReadOnlyList<(double X, double Y)> First, IReadOnlyList<(double X, double Y)> Second)
    {
        int Count = 0;
        for (int A = 1; A < First.Count; A++)
        {
            for (int B = 1; B < Second.Count; B++)
            {
                if (SegmentsCross(First[A - 1], First[A], Second[B - 1], Second[B]))
                {
                    Count++;
                }
            }
        }

        return Count;
    }

    private static bool SegmentsCross((double X, double Y) A, (double X, double Y) B, (double X, double Y) C, (double X, double Y) D)
    {
        double Denominator = ((B.X - A.X) * (D.Y - C.Y)) - ((B.Y - A.Y) * (D.X - C.X));
        if (Math.Abs(Denominator) < 1e-6)
        {
            return false;
        }

        double First = (((C.X - A.X) * (D.Y - C.Y)) - ((C.Y - A.Y) * (D.X - C.X))) / Denominator;
        double Second = (((C.X - A.X) * (B.Y - A.Y)) - ((C.Y - A.Y) * (B.X - A.X))) / Denominator;
        return First > 0.001 && First < 0.999 && Second > 0.001 && Second < 0.999;
    }

    [Test]
    public void CompleteSample_WebShopRightSide_UsesTheSideGrid()
    {
        var (Layout, Routes) = LayOut(COMPLETE_SAMPLE);
        var WebShop = Layout.Nodes["webShop"];
        double Right = WebShop.AbsoluteX + WebShop.Width;
        double[] Ports = EndsOn(Routes, "webShop")
            .Where(Point => Math.Abs(Point.X - Right) < 1.0)
            .Select(Point => Point.Y - WebShop.AbsoluteY)
            .OrderBy(Offset => Offset).ToArray();
        double[] Grid = DiagramGridMetrics.SideGridOffsets(WebShop.Height);
        Assert.Multiple(() =>
        {
            Assert.That(Ports, Has.Length.EqualTo(3));
            foreach (double Port in Ports)
            {
                Assert.That(Grid.Any(Point => Math.Abs(Point - Port) < 0.5), Is.True, Port.ToString("0.##"));
            }

            for (int Index = 1; Index < Ports.Length; Index++)
            {
                Assert.That(Ports[Index] - Ports[Index - 1], Is.GreaterThanOrEqualTo(SPACING - 0.5));
            }
        });
    }

    [Test]
    public void CompleteSample_EveryRectangularPort_LiesOnTheSideGrid()
    {
        var (Layout, Routes) = LayOut(COMPLETE_SAMPLE);
        List<string> Failures = new List<string>();
        foreach (var Route in Routes)
        {
            CheckPort(Layout, Route.Relationship.SourceId, Route.SvgPoints[0], Route, Failures);
            CheckPort(Layout, Route.Relationship.TargetId, Route.SvgPoints[^1], Route, Failures);
        }

        Assert.That(Failures, Is.Empty, string.Join(Environment.NewLine, Failures));
    }

    private static void CheckPort(EnterpriseLayoutResult Layout, string NodeId, (double X, double Y) Point,
        EnterpriseRoutedEdge Route, List<string> Failures)
    {
        if (!Layout.Nodes.TryGetValue(NodeId, out var Node) || Node.Element == null) return;
        if (EnterpriseElementCatalog.Elements[Node.Element.ElementType].BoxKind
            is not (EnterpriseBoxKind.Rectangle or EnterpriseBoxKind.Object or EnterpriseBoxKind.Box3d)) return;
        double Left = Node.AbsoluteX, Top = Node.AbsoluteY, Right = Left + Node.Width, Bottom = Top + Node.Height;
        double Offset, Length;
        if (Math.Abs(Point.Y - Top) < 1.0 || Math.Abs(Point.Y - Bottom) < 1.0)
        {
            Offset = Point.X - Left;
            Length = Node.Width;
        }
        else if (Math.Abs(Point.X - Left) < 1.0 || Math.Abs(Point.X - Right) < 1.0)
        {
            Offset = Point.Y - Top;
            Length = Node.Height;
        }
        else
        {
            return;
        }

        if (!DiagramGridMetrics.SideGridOffsets(Length).Any(Grid => Math.Abs(Grid - Offset) < 0.5))
            Failures.Add($"{Route.Relationship.SourceId}->{Route.Relationship.TargetId} at {NodeId}: offset {Offset:0.##} of {Length:0.##}");
    }

    private static IEnumerable<(double X, double Y)> EndsOn(IEnumerable<EnterpriseRoutedEdge> Routes, string NodeId)
    {
        foreach (var Route in Routes)
        {
            if (Route.Relationship.SourceId == NodeId) yield return Route.SvgPoints[0];
            if (Route.Relationship.TargetId == NodeId) yield return Route.SvgPoints[^1];
        }
    }

    private static (EnterpriseLayoutResult Layout, List<EnterpriseRoutedEdge> Routes) LayOut(string Diagram)
    {
        var Document = EnterpriseValidator.Analyze(Diagram).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        // Route the way the SVG renderer does, reusing the layout's routing context.
        var Routes = (Layout.RoutingContext == null
            ? EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes)
            : EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes, Layout.RoutingContext)).ToList();
        return (Layout, Routes);
    }

    private static string Bounds(EnterpriseLayoutNode Node) =>
        $"({Node.AbsoluteX:0.##},{Node.AbsoluteY:0.##}) {Node.Width:0.##}x{Node.Height:0.##}";

    private static string Describe(IEnumerable<(double X, double Y)> Points) =>
        string.Join(" ", Points.Select(Point => $"({Point.X:0.##},{Point.Y:0.##})"));
}
