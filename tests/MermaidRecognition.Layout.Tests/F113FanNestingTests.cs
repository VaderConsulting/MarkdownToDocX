using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F113FanNestingTests
{
    private const string F113_SOURCE = """
        flowchart LR
        Start[Start]
        End[End]
        Start --> B1 --> End
        Start --> B2 --> End
        Start --> B3 --> End
        Start --> B4 --> End
        Start --> B5 --> End
        Start --> B6 --> End
        Start --> B7 --> End
        Start --> B8 --> End
        Start --> B9 --> End
        Start --> B10 --> End
        Start --> B11 --> End
        Start --> B12 --> End
        Start --> B13 --> End
        Start --> B14 --> End
        Start --> B15 --> End
        Start --> B16 --> End
        Start --> B17 --> End
        Start --> B18 --> End
        Start --> B19 --> End
        Start --> B20 --> End
        """;

    [Test]
    public void TryLayout_F113_FanRoutes_UseNestedOrthogonalTracksWithoutCrossings()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F113_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        List<FlowchartLayoutEdgeRoute> FanInRoutes = [];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (string.Equals(Route.TargetId, "End", StringComparison.Ordinal))
            {
                FanInRoutes.Add(Route);
            }
        }

        Assert.That(FanInRoutes, Has.Count.EqualTo(20));

        List<List<(double X, double Y)>> Paths = [];
        foreach (FlowchartLayoutEdgeRoute Route in FanInRoutes)
        {
            List<(double X, double Y)> Points = [];
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Points.Add((Point.X, Point.Y));
            }

            Assert.That(Points, Has.Count.GreaterThanOrEqualTo(4), Route.SourceId + "->End should use a nested corridor route");
            Assert.That(Route.EntrySide, Is.EqualTo("left"), Route.SourceId + "->End should enter End from the left face");
            Paths.Add(Points);
        }

        for (int LeftIndex = 0; LeftIndex < Paths.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Paths.Count; RightIndex++)
            {
                if (SegmentsCrossPolylines(Paths[LeftIndex], Paths[RightIndex]))
                {
                    TestContext.WriteLine(
                        FanInRoutes[LeftIndex].SourceId + "->End: "
                        + string.Join(" ", Paths[LeftIndex].Select(Point => $"({Point.X:F0},{Point.Y:F0})")));
                    TestContext.WriteLine(
                        FanInRoutes[RightIndex].SourceId + "->End: "
                        + string.Join(" ", Paths[RightIndex].Select(Point => $"({Point.X:F0},{Point.Y:F0})")));
                }

                Assert.That(
                    SegmentsCrossPolylines(Paths[LeftIndex], Paths[RightIndex]),
                    Is.False,
                    FanInRoutes[LeftIndex].SourceId + "->End crosses " + FanInRoutes[RightIndex].SourceId + "->End");
            }
        }
    }

    [Test]
    public void TryLayout_F113_FanOutRoutes_LeaveStartFromTopOrBottomFace()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F113_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!string.Equals(Route.SourceId, "Start", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.That(
                Route.ExitSide,
                Is.EqualTo("top").Or.EqualTo("bottom"),
                Route.TargetId + " should leave Start from the top or bottom face");
            Assert.That(Route.Points, Has.Count.EqualTo(4), Route.TargetId + " fan-out should use a four-point nested route");
        }
    }

    private static bool SegmentsCrossPolylines(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                if (SegmentsCross(
                    Left[LeftIndex - 1].X,
                    Left[LeftIndex - 1].Y,
                    Left[LeftIndex].X,
                    Left[LeftIndex].Y,
                    Right[RightIndex - 1].X,
                    Right[RightIndex - 1].Y,
                    Right[RightIndex].X,
                    Right[RightIndex].Y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsCross(
        double Ax1,
        double Ay1,
        double Ax2,
        double Ay2,
        double Bx1,
        double By1,
        double Bx2,
        double By2)
    {
        double Denominator = ((Bx2 - Bx1) * (Ay2 - Ay1)) - ((By2 - By1) * (Ax2 - Ax1));
        if (Math.Abs(Denominator) < 1e-9)
        {
            return false;
        }

        double Ta = (((Bx1 - Ax1) * (Ay2 - Ay1)) - ((By1 - Ay1) * (Ax2 - Ax1))) / Denominator;
        double Tb = (((Bx1 - Ax1) * (By2 - By1)) - ((By1 - Ay1) * (Bx2 - Bx1))) / Denominator;
        return Ta > 0.01 && Ta < 0.99 && Tb > 0.01 && Tb < 0.99;
    }
}
