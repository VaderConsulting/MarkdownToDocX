using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// The shared endpoint helper must clip every shape to its visible outline, not the bounding box.
/// </summary>
[TestFixture]
public sealed class FlowchartShapeBoundaryAttachTests
{
    [Test]
    public void Stadium_SideRayHitsTheCapNotTheCorner()
    {
        FlowchartLayoutNodeBounds Bounds = Box(0.0, 0.0, 120.0, 40.0);
        (double X, double Y) Attach = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            "stadium",
            80.0,
            0.0,
            EdgeEndpointMarker.None);
        Assert.That(Attach.X, Is.EqualTo(60.0).Within(0.2));
        Assert.That(Attach.Y, Is.EqualTo(0.0).Within(0.2));
        AssertPointOnStadium(Bounds, Attach.X, Attach.Y);
    }

    [Test]
    public void Stadium_DiagonalRayHitsTheArcNotTheBoundingBoxCorner()
    {
        FlowchartLayoutNodeBounds Bounds = Box(0.0, 0.0, 120.0, 40.0);
        (double X, double Y) Attach = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            "stadium",
            80.0,
            80.0,
            EdgeEndpointMarker.None);
        double CornerDistance = Distance(Attach.X, Attach.Y, 60.0, 20.0);
        Assert.That(CornerDistance, Is.GreaterThan(8.0));
        AssertPointOnStadium(Bounds, Attach.X, Attach.Y);
    }

    [Test]
    public void Diamond_RayHitsASlopeNotTheBoundingBox()
    {
        FlowchartLayoutNodeBounds Bounds = Box(0.0, 0.0, 80.0, 80.0);
        (double X, double Y) Attach = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            "diamond",
            40.0,
            40.0,
            EdgeEndpointMarker.None);
        Assert.That(Math.Abs(Attach.X) + Math.Abs(Attach.Y), Is.EqualTo(40.0).Within(0.4));
    }

    [Test]
    public void Circle_RayHitsTheRim()
    {
        FlowchartLayoutNodeBounds Bounds = Box(0.0, 0.0, 60.0, 60.0);
        (double X, double Y) Attach = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            "circle",
            40.0,
            0.0,
            EdgeEndpointMarker.None);
        Assert.That(Distance(Attach.X, Attach.Y, 0.0, 0.0), Is.EqualTo(30.0).Within(0.2));
    }

    [Test]
    public void Cylinder_VerticalRayHitsTheRim()
    {
        FlowchartLayoutNodeBounds Bounds = Box(0.0, 0.0, 80.0, 50.0);
        (double X, double Y) Attach = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            "cylinder",
            0.0,
            80.0,
            EdgeEndpointMarker.None);
        Assert.That(Attach.X, Is.EqualTo(0.0).Within(0.2));
        Assert.That(Attach.Y, Is.EqualTo(25.0).Within(0.4));
    }

    [Test]
    public void Rounded_CornerRayHitsTheArcNotTheBoundingBoxCorner()
    {
        FlowchartLayoutNodeBounds Bounds = Box(0.0, 0.0, 80.0, 40.0);
        (double X, double Y) Attach = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            "rounded",
            40.0,
            20.0,
            EdgeEndpointMarker.None);
        double CornerDistance = Distance(Attach.X, Attach.Y, 40.0, 20.0);
        Assert.That(CornerDistance, Is.GreaterThan(1.5));
        double Radius = FlowchartShapePathGenerator.FLOWCHART_ROUNDED_RECT_RADIUS_PX;
        double ArcCentreX = 40.0 - Radius;
        double ArcCentreY = 20.0 - Radius;
        Assert.That(Distance(Attach.X, Attach.Y, ArcCentreX, ArcCentreY), Is.EqualTo(Radius).Within(0.6));
    }

    private static FlowchartLayoutNodeBounds Box(double CenterX, double CenterY, double Width, double Height)
    {
        return new FlowchartLayoutNodeBounds
        {
            NodeId = "n",
            CenterX = CenterX,
            CenterY = CenterY,
            Width = Width,
            Height = Height
        };
    }

    private static void AssertPointOnStadium(FlowchartLayoutNodeBounds Bounds, double X, double Y)
    {
        double HalfWidth = Bounds.Width / 2.0;
        double Radius = Bounds.Height / 2.0;
        double Flat = HalfWidth - Radius;
        if (Math.Abs(X) <= Flat + 0.2)
        {
            Assert.That(Math.Abs(Y), Is.EqualTo(Radius).Within(0.4));
            return;
        }

        double CapX = X >= 0.0 ? Flat : -Flat;
        Assert.That(Distance(X, Y, CapX, 0.0), Is.EqualTo(Radius).Within(0.4));
    }

    private static double Distance(double LeftX, double LeftY, double RightX, double RightY)
    {
        double Dx = LeftX - RightX;
        double Dy = LeftY - RightY;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }
}
