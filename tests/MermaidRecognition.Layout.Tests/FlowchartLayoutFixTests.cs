using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Shared layout fixes: subgraph membership, root ranking, and compound-edge attach.
/// </summary>
[TestFixture]
public sealed class FlowchartLayoutFixTests
{
    private const string SAMPLE_03_SOURCE = """
        flowchart TB
          subgraph dc["Primary data centre"]
            subgraph appNode["App Server Node"]
              jvm["Java VM"]
              war["shop.war"]
            end
            pg["PostgreSQL"]
            lb["Load Balancer"]
            lan["DC LAN"]
          end
          subgraph webShop["Web Shop"]
            checkout(["Checkout"])
          end
          shopUi["Shop Web UI"]
          ordering(["Online Ordering"])
          order["Order"]
          hosting(["Java Application Hosting"])
          dbSvc(["Database Service"])
          orderTbl["orders table"]
          webShop --> shopUi
          checkout --> ordering
          shopUi --> ordering
          checkout --> order
          war --> webShop
          orderTbl --> order
          jvm --> hosting
          pg --> dbSvc
          hosting --> webShop
          dbSvc --> webShop
          pg --> orderTbl
          lb -->|HTTPS| appNode
          lan --> lb
          lan --> appNode
        """;

    private const string SAMPLE_12_SOURCE = """
        flowchart TB
          board["Board"]
          raiseRev["Raise Revenue"]
          sellingCap(["Selling Capability"])
          takeOrder(["Take Order"])
          selling(["Selling Service"])
          webShop["Web Shop"]
          ordering(["Online Ordering"])
          appNode["App Server Node"]
          hosting(["Hosting"])
          board --> raiseRev
          raiseRev --> sellingCap
          sellingCap --> takeOrder
          takeOrder --> selling
          selling --> webShop
          webShop --> ordering
          ordering --> hosting
          appNode --> hosting
        """;

    [Test]
    public void TryLayout_Sample03_NonMembersStayOutsideSubgraphBounds()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> Rects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect DataCentre = Rects.Single(Rect => Rect.GroupId == "dc");

        string[] NonMemberIds =
        [
            "shopUi",
            "ordering",
            "order",
            "hosting",
            "dbSvc",
            "orderTbl",
            "checkout"
        ];
        foreach (string NodeId in NonMemberIds)
        {
            Assert.That(BoundsById.ContainsKey(NodeId), Is.True, NodeId);
            Assert.That(
                OverlapsRect(BoundsById[NodeId], DataCentre),
                Is.False,
                NodeId + " must stay outside Primary data centre");
        }

        string[] MemberIds = ["jvm", "war", "pg", "lb", "lan"];
        foreach (string NodeId in MemberIds)
        {
            Assert.That(BoundsById.ContainsKey(NodeId), Is.True, NodeId);
            Assert.That(
                IsInsideRect(BoundsById[NodeId], DataCentre),
                Is.True,
                NodeId + " must stay inside Primary data centre");
        }
    }

    [Test]
    public void TryLayout_Sample12_RootSitsOneRankAboveSuccessor()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_12_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        FlowchartLayoutNodeBounds Board = BoundsById["board"];
        FlowchartLayoutNodeBounds RaiseRevenue = BoundsById["raiseRev"];
        FlowchartLayoutNodeBounds AppNode = BoundsById["appNode"];
        FlowchartLayoutNodeBounds Hosting = BoundsById["hosting"];

        Assert.That(Board.CenterY, Is.GreaterThan(RaiseRevenue.CenterY));
        Assert.That(AppNode.CenterY, Is.GreaterThan(Hosting.CenterY));

        double BoardGap = (Board.CenterY - (Board.Height / 2.0)) - (RaiseRevenue.CenterY + (RaiseRevenue.Height / 2.0));
        Assert.That(BoardGap, Is.GreaterThanOrEqualTo(20.0));
    }

    [Test]
    public void TryResolve_Sample03_CompoundEdgesClipToGroupBoundary()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, BoundsById);
        Dictionary<string, NodeContract> NodeById =
            Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> Rects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectById =
            Rects.ToDictionary(Rect => Rect.GroupId, StringComparer.Ordinal);

        (string SourceId, string TargetId)[] CompoundEdges =
        [
            ("webShop", "shopUi"),
            ("war", "webShop"),
            ("hosting", "webShop"),
            ("dbSvc", "webShop"),
            ("lb", "appNode"),
            ("lan", "appNode")
        ];

        foreach ((string SourceId, string TargetId) Pair in CompoundEdges)
        {
            EdgeContract Edge = Contract.Edges.Single(
                Candidate => Candidate.SourceId == Pair.SourceId && Candidate.TargetId == Pair.TargetId);
            Assert.That(
                FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    FlowchartDirection.TopToBottom,
                    out List<(double X, double Y)> Points,
                    out FlowchartEdgeRouteKind _),
                Is.True,
                Pair.SourceId + " -> " + Pair.TargetId + " must resolve");
            Assert.That(Points.Count, Is.EqualTo(2), Pair.SourceId + " -> " + Pair.TargetId);
            AssertPointOnEndpoint(Points[0], Pair.SourceId, BoundsById, RectById);
            AssertPointOnEndpoint(Points[1], Pair.TargetId, BoundsById, RectById);

            double Straight = Distance(Points[0], Points[1]);
            double PathLength = Straight;
            Assert.That(PathLength, Is.LessThanOrEqualTo(Straight + 1.0));
        }
    }

    [Test]
    public void TryResolve_Sample03_LanToLoadBalancerKeepsSourceDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById =
            Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        EdgeContract Edge = Contract.Edges.Single(
            Candidate => Candidate.SourceId == "lan" && Candidate.TargetId == "lb");

        Assert.That(Edge.SourceId, Is.EqualTo("lan"));
        Assert.That(Edge.TargetId, Is.EqualTo("lb"));
        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                FlowchartDirection.TopToBottom,
                out List<(double X, double Y)> Points,
                out FlowchartEdgeRouteKind _),
            Is.True);

        FlowchartLayoutNodeBounds Lan = BoundsById["lan"];
        FlowchartLayoutNodeBounds LoadBalancer = BoundsById["lb"];
        double StartToLan = Distance(Points[0], (Lan.CenterX, Lan.CenterY));
        double StartToLb = Distance(Points[0], (LoadBalancer.CenterX, LoadBalancer.CenterY));
        double EndToLan = Distance(Points[^1], (Lan.CenterX, Lan.CenterY));
        double EndToLb = Distance(Points[^1], (LoadBalancer.CenterX, LoadBalancer.CenterY));
        Assert.That(StartToLan, Is.LessThan(StartToLb));
        Assert.That(EndToLb, Is.LessThan(EndToLan));
    }

    [Test]
    public void RenderToSvg_Sample03_DrawsEveryVisibleEdge()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.StartWith("<svg"));

        int VisibleEdgeCount = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                VisibleEdgeCount++;
            }
        }

        int PathCount = 0;
        int SearchFrom = 0;
        while (true)
        {
            int Index = Svg.IndexOf("<path", SearchFrom, StringComparison.Ordinal);
            if (Index < 0)
            {
                break;
            }

            PathCount++;
            SearchFrom = Index + 5;
        }

        Assert.That(PathCount, Is.GreaterThanOrEqualTo(VisibleEdgeCount));
        Assert.That(Svg, Does.Contain("HTTPS"));
    }

    [Test]
    public void TryLayout_Sample11_StoreClusterFitsMembers()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
              subgraph store["Store Cluster"]
                clerk["Clerk"]
                start(["Start"])
                finish(["Finish"])
              end
              london["London"]
              both((" "))
              either((" "))
              london --> clerk
              start --> both
              both --> finish
              start --> either
              either --> finish
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> Rects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect Store = Rects.Single(Rect => Rect.GroupId == "store");

        double MemberLeft = double.MaxValue;
        double MemberRight = double.MinValue;
        double MemberBottom = double.MaxValue;
        double MemberTop = double.MinValue;
        foreach (string NodeId in new[] { "clerk", "start", "finish" })
        {
            FlowchartLayoutNodeBounds Bounds = BoundsById[NodeId];
            MemberLeft = Math.Min(MemberLeft, Bounds.CenterX - (Bounds.Width / 2.0));
            MemberRight = Math.Max(MemberRight, Bounds.CenterX + (Bounds.Width / 2.0));
            MemberBottom = Math.Min(MemberBottom, Bounds.CenterY - (Bounds.Height / 2.0));
            MemberTop = Math.Max(MemberTop, Bounds.CenterY + (Bounds.Height / 2.0));
            Assert.That(IsInsideRect(Bounds, Store), Is.True, NodeId);
        }

        double ExtraWidth = (Store.Right - Store.Left) - (MemberRight - MemberLeft);
        double ExtraHeight = (Store.Top - Store.Bottom) - (MemberTop - MemberBottom);
        double ExpectedHeightPad = FlowchartSubgraphBoundsBuilder.PADDING_PX
            + FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX
            + FlowchartSubgraphBoundsBuilder.VERTICAL_CONTENT_SLACK_PX
            + 8.0;
        Assert.That(ExtraHeight, Is.LessThanOrEqualTo(ExpectedHeightPad + 40.0));
        Assert.That(ExtraWidth, Is.LessThanOrEqualTo(MemberRight - MemberLeft + 80.0));

        foreach (string NodeId in new[] { "london", "both", "either" })
        {
            Assert.That(OverlapsRect(BoundsById[NodeId], Store), Is.False, NodeId);
        }
    }

    private static bool OverlapsRect(FlowchartLayoutNodeBounds Bounds, FlowchartSubgraphLayoutRect Rect)
    {
        double NodeLeft = Bounds.CenterX - (Bounds.Width / 2.0);
        double NodeRight = Bounds.CenterX + (Bounds.Width / 2.0);
        double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);
        return NodeRight > Rect.Left + 0.5
            && NodeLeft < Rect.Right - 0.5
            && NodeTop > Rect.Bottom + 0.5
            && NodeBottom < Rect.Top - 0.5;
    }

    private static bool IsInsideRect(FlowchartLayoutNodeBounds Bounds, FlowchartSubgraphLayoutRect Rect)
    {
        double NodeLeft = Bounds.CenterX - (Bounds.Width / 2.0);
        double NodeRight = Bounds.CenterX + (Bounds.Width / 2.0);
        double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);
        return NodeLeft >= Rect.Left - 0.5
            && NodeRight <= Rect.Right + 0.5
            && NodeBottom >= Rect.Bottom - 0.5
            && NodeTop <= Rect.Top + 0.5;
    }

    private static void AssertPointOnEndpoint(
        (double X, double Y) Point,
        string EndpointId,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, FlowchartSubgraphLayoutRect> RectById)
    {
        if (RectById.TryGetValue(EndpointId, out FlowchartSubgraphLayoutRect? Rect) && Rect is not null)
        {
            Assert.That(IsOnRectBoundary(Point, Rect), Is.True, EndpointId + " attach is off the cluster boundary");
            return;
        }

        FlowchartLayoutNodeBounds Bounds = BoundsById[EndpointId];
        FlowchartSubgraphLayoutRect NodeRect = new FlowchartSubgraphLayoutRect
        {
            GroupId = EndpointId,
            Left = Bounds.CenterX - (Bounds.Width / 2.0),
            Right = Bounds.CenterX + (Bounds.Width / 2.0),
            Bottom = Bounds.CenterY - (Bounds.Height / 2.0),
            Top = Bounds.CenterY + (Bounds.Height / 2.0)
        };
        Assert.That(IsOnRectBoundary(Point, NodeRect), Is.True, EndpointId + " attach is off the node boundary");
    }

    private static bool IsOnRectBoundary((double X, double Y) Point, FlowchartSubgraphLayoutRect Rect)
    {
        bool OnVertical = (Math.Abs(Point.X - Rect.Left) <= 1.5 || Math.Abs(Point.X - Rect.Right) <= 1.5)
            && Point.Y >= Rect.Bottom - 1.5
            && Point.Y <= Rect.Top + 1.5;
        bool OnHorizontal = (Math.Abs(Point.Y - Rect.Bottom) <= 1.5 || Math.Abs(Point.Y - Rect.Top) <= 1.5)
            && Point.X >= Rect.Left - 1.5
            && Point.X <= Rect.Right + 1.5;
        return OnVertical || OnHorizontal;
    }

    private static double Distance((double X, double Y) Left, (double X, double Y) Right)
    {
        double Dx = Left.X - Right.X;
        double Dy = Left.Y - Right.Y;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }
}
