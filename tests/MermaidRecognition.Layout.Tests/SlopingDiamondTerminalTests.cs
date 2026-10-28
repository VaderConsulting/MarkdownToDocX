using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class SlopingDiamondTerminalTests
{
    [TestCase(-1, -1, false)]
    [TestCase(1, -1, false)]
    [TestCase(-1, 1, false)]
    [TestCase(1, 1, false)]
    [TestCase(-1, -1, true)]
    [TestCase(1, -1, true)]
    [TestCase(-1, 1, true)]
    [TestCase(1, 1, true)]
    public void Normalise_SlopingEdge_KeepsSurfaceNormalAndAxisAlignedBody(int SideX, int SideY, bool Inbound)
    {
        FlowchartLayoutNodeBounds Diamond = new FlowchartLayoutNodeBounds { NodeId = "Diamond", CenterX = 400, CenterY = 400, Width = 240, Height = 180 };
        FlowchartLayoutNodeBounds Other = new FlowchartLayoutNodeBounds { NodeId = "Other", CenterX = 400 + (SideX * 300), CenterY = 400 + (SideY * 300), Width = 120, Height = 60 };
        List<(double X, double Y)> Points =
        [
            (Diamond.CenterX + (SideX * Diamond.Width / 4), Diamond.CenterY + (SideY * Diamond.Height / 4)),
            (Other.CenterX, Diamond.CenterY + (SideY * Diamond.Height / 4)),
            (Other.CenterX, Other.CenterY - (SideY * Other.Height / 2))
        ];
        if (Inbound)
        {
            Points.Reverse();
        }

        FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { SourceId = Inbound ? "Other" : "Diamond", TargetId = Inbound ? "Diamond" : "Other" };
        foreach ((double X, double Y) Point in Points)
        {
            Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }

        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        Layout.EdgeRoutesMutable.Add(Route);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new Dictionary<string, FlowchartLayoutNodeBounds> { ["Diamond"] = Diamond, ["Other"] = Other };
        Dictionary<string, NodeContract> Nodes = new Dictionary<string, NodeContract> { ["Diamond"] = new() { Id = "Diamond", Shape = "diamond" }, ["Other"] = new() { Id = "Other", Shape = "rect" } };
        FlowchartGridTerminalNormalizer.Apply(Layout, Bounds, Nodes);
        List<(double X, double Y)> Actual = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
        if (Inbound)
        {
            Actual.Reverse();
        }

        Assert.That(Actual[0], Is.EqualTo(Inbound ? Points[^1] : Points[0]));
        Assert.That(FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Diamond, Actual[0].X, Actual[0].Y, out double Nx, out double Ny), Is.True);
        double Dx = Actual[1].X - Actual[0].X;
        double Dy = Actual[1].Y - Actual[0].Y;
        Assert.That((Dx * Ny) - (Dy * Nx), Is.EqualTo(0).Within(0.00001));
        Assert.That((Dx * Nx) + (Dy * Ny), Is.GreaterThanOrEqualTo(50));
        Assert.That(Actual[^1], Is.EqualTo(Inbound ? Points[0] : Points[^1]));
        Assert.That(Actual[^2].X, Is.EqualTo(Actual[^1].X));
        for (int Index = 2; Index < Actual.Count; Index++)
        {
            Assert.That(Math.Abs(Actual[Index].X - Actual[Index - 1].X) < 0.5
                || Math.Abs(Actual[Index].Y - Actual[Index - 1].Y) < 0.5, Is.True);
        }
        string SvgPath = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(Actual, 20);
        Assert.That(SvgPath, Does.Contain("C"), "The normal run joins its track with a rounded bend.");
    }
}