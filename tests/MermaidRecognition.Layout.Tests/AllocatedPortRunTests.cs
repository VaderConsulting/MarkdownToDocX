using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Exact fractional terminal tracks are prepared before scoring, without retaining fractional middle tracks.</summary>
[TestFixture]
public sealed class AllocatedPortRunTests
{
    /// <summary>Both terminal rays retain their exact coordinate in every cardinal direction.</summary>
    /// <param name="QuarterTurns">Rotation of a rightward departure through all four sides.</param>
    [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
    public void TerminalRuns_StayOnAllocatedCoordinates_WhileMiddleTracksAreQuantised(int QuarterTurns)
    {
        (double X, double Y) Rotate((double X, double Y) Point)
        {
            for (int Turn = 0; Turn < QuarterTurns; Turn++)
            {
                Point = (-Point.Y, Point.X);
            }

            return Point;
        }
        List<(double X, double Y)> Original = [.. new List<(double X, double Y)>
            { (75, 22.5), (120.25, 22.5), (120.25, 100.25), (210.25, 100.25), (210.25, 157.5), (300, 157.5) }
            .Select(Rotate)];
        List<(double X, double Y)> Drawn = DiagramGridRouteBuilder.PrepareDrawnGeometry(Original);
        Assert.That(Drawn[0], Is.EqualTo(Original[0]));
        Assert.That(Drawn[^1], Is.EqualTo(Original[^1]));
        AssertNormal(Original[0], Original[1], Drawn[0], Drawn[1]);
        AssertNormal(Original[^1], Original[^2], Drawn[^1], Drawn[^2]);
        for (int Index = 2; Index < Drawn.Count - 2; Index++)
        {
            Assert.That(Drawn[Index].X, Is.EqualTo(Math.Round(Drawn[Index].X)));
            Assert.That(Drawn[Index].Y, Is.EqualTo(Math.Round(Drawn[Index].Y)));
        }
        Assert.That(DiagramGridRouteBuilder.PrepareDrawnGeometry(Drawn), Is.EqualTo(Drawn),
            "Validation and publication must not quantise the geometry a second time.");
    }

    /// <summary>Three independently allocated ports on one side keep square runs into the next rank.</summary>
    [Test]
    public void ThreePortsOnOneSide_RouteToThreeNodesInTheNextRank()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nHub[Hub] --> A[First]\nHub --> B[Second]\nHub --> C[Third]");
        FlowchartLayoutNodeBounds Hub = new FlowchartLayoutNodeBounds { NodeId = "Hub", CenterX = 0, CenterY = 0, Width = 150, Height = 90 };
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new Dictionary<string, FlowchartLayoutNodeBounds> { ["Hub"] = Hub };
        string[] Leaves = ["A", "B", "C"];
        for (int Index = 0; Index < Leaves.Length; Index++)
        {
            Bounds[Leaves[Index]] = new() { NodeId = Leaves[Index], CenterX = (Index - 1) * 180, CenterY = -300, Width = 150, Height = 90 };
        }

        for (int Index = 0; Index < Leaves.Length; Index++)
        {
            double PortX = -75 + ((Index + 1) * 150d / 4);
            FlowchartLayoutNodeBounds Target = Bounds[Leaves[Index]];
            List<(double X, double Y)> Route = DiagramGridRouteBuilder.BuildRoute(PortX, -45, Hub, "rect", "bottom",
                Target.CenterX, -255, Target, "rect", "top", Bounds, "Hub", Target.NodeId,
                Contract, FlowchartDirection.TopToBottom, Horizontal: false);
            Assert.That(Route, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(Route[0], Is.EqualTo((PortX, -45d)));
            Assert.That(Route[1].X, Is.EqualTo(PortX));
            Assert.That(Route[^2].X, Is.EqualTo(Target.CenterX));
            Assert.That(DiagramGridRouteBuilder.PathIsValid(Route, Bounds, "Hub", Target.NodeId, Contract), Is.True);
        }
    }

    /// <summary>Facing fractional slots retain their coordinates while an existing bend track is shortened.</summary>
    [Test]
    public void FacingOffCentreTerminalRuns_SeparateByMovingTheFirstBendOnly()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\nS[Source] --> A[First]\nB[Second] --> T[Target]");
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = [];
        foreach ((string, double, double) Item in new[] { ("S", -75d, 0d), ("A", 375d, 150d), ("B", -375d, -150d), ("T", 195d, 0d) })
        {
            FlowchartLayoutNodeBounds Box = new FlowchartLayoutNodeBounds { NodeId = Item.Item1, CenterX = Item.Item2, CenterY = Item.Item3, Width = 150, Height = 90 };
            Bounds.Add(Box.NodeId, Box); Layout.NodeBoundsMutable.Add(Box);
        }
        FlowchartLayoutEdgeRoute First = new FlowchartLayoutEdgeRoute { EdgeId = Contract.Edges[0].Id!, SourceId = "S", TargetId = "A",
            ExitSide = "right", EntrySide = "left", ExitNormalX = 1, EntryNormalX = -1 };
        FlowchartLayoutEdgeRoute Second = new FlowchartLayoutEdgeRoute { EdgeId = Contract.Edges[1].Id!, SourceId = "B", TargetId = "T",
            ExitSide = "right", EntrySide = "left", ExitNormalX = 1, EntryNormalX = -1 };
        foreach ((double, double) P in new[] { (0d, 7.5), (80d, 7.5), (80d, 150d), (300d, 150d) })
        {
            First.Points.Add(new() { X = P.Item1, Y = P.Item2 });
        }

        foreach ((double, double) P in new[] { (-300d, -150d), (40d, -150d), (40d, 7.5), (120d, 7.5) })
        {
            Second.Points.Add(new() { X = P.Item1, Y = P.Item2 });
        }

        Layout.EdgeRoutesMutable.AddRange([First, Second]);
        (double, double, double, double)[] Ports = [.. Layout.EdgeRoutes.Select(Route => (Route.Points[0].X, Route.Points[0].Y, Route.Points[^1].X, Route.Points[^1].Y))];
        int Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
        Assert.That(FlowchartTerminalTrackRepair.Apply(Contract, Layout, [], Bounds), Is.GreaterThan(0));
        Assert.That(FlowchartTerminalTrackRepair.Conflicts(Layout), Is.Empty);
        Assert.That(Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Is.EqualTo(Bends));
        Assert.That(Layout.EdgeRoutes.Select(Route => (Route.Points[0].X, Route.Points[0].Y, Route.Points[^1].X, Route.Points[^1].Y)), Is.EqualTo(Ports));
    }

    /// <summary>Checks exact collinearity with the allocator's ray, rather than a pixel tolerance.</summary>
    private static void AssertNormal((double X, double Y) Port, (double X, double Y) Stub,
        (double X, double Y) DrawnPort, (double X, double Y) DrawnStub)
    {
        Assert.That(((Stub.X - Port.X) * (DrawnStub.Y - DrawnPort.Y))
            - ((Stub.Y - Port.Y) * (DrawnStub.X - DrawnPort.X)), Is.EqualTo(0));
    }
}
