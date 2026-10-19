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
            for (int Turn = 0; Turn < QuarterTurns; Turn++) Point = (-Point.Y, Point.X);
            return Point;
        }
        var Original = new List<(double X, double Y)>
            { (75, 22.5), (120.25, 22.5), (120.25, 100.25), (210.25, 100.25), (210.25, 157.5), (300, 157.5) }
            .Select(Rotate).ToList();
        var Drawn = DiagramGridRouteBuilder.PrepareDrawnGeometry(Original);
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
        var Contract = MermaidRecognizer.Parse("flowchart TB\nHub[Hub] --> A[First]\nHub --> B[Second]\nHub --> C[Third]");
        var Hub = new FlowchartLayoutNodeBounds { NodeId = "Hub", CenterX = 0, CenterY = 0, Width = 150, Height = 90 };
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds> { ["Hub"] = Hub };
        string[] Leaves = ["A", "B", "C"];
        for (int Index = 0; Index < Leaves.Length; Index++)
            Bounds[Leaves[Index]] = new() { NodeId = Leaves[Index], CenterX = (Index - 1) * 180, CenterY = -300, Width = 150, Height = 90 };
        for (int Index = 0; Index < Leaves.Length; Index++)
        {
            double PortX = -75 + (Index + 1) * 150d / 4;
            var Target = Bounds[Leaves[Index]];
            var Route = DiagramGridRouteBuilder.BuildRoute(PortX, -45, Hub, "rect", "bottom",
                Target.CenterX, -255, Target, "rect", "top", Bounds, "Hub", Target.NodeId,
                Contract, FlowchartDirection.TopToBottom, Horizontal: false);
            Assert.That(Route, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(Route[0], Is.EqualTo((PortX, -45d)));
            Assert.That(Route[1].X, Is.EqualTo(PortX));
            Assert.That(Route[^2].X, Is.EqualTo(Target.CenterX));
            Assert.That(DiagramGridRouteBuilder.PathIsValid(Route, Bounds, "Hub", Target.NodeId, Contract), Is.True);
        }
    }

    /// <summary>Checks exact collinearity with the allocator's ray, rather than a pixel tolerance.</summary>
    private static void AssertNormal((double X, double Y) Port, (double X, double Y) Stub,
        (double X, double Y) DrawnPort, (double X, double Y) DrawnStub)
    {
        Assert.That((Stub.X - Port.X) * (DrawnStub.Y - DrawnPort.Y)
            - (Stub.Y - Port.Y) * (DrawnStub.X - DrawnPort.X), Is.EqualTo(0));
    }
}
