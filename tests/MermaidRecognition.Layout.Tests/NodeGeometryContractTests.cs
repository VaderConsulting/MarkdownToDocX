using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the renderer-owned geometry contract without selecting shape names in routing code.</summary>
[TestFixture]
public sealed class NodeGeometryContractTests
{
    /// <summary>Unspecified shapes use one rectangle for visual extent, connections and collisions.</summary>
    [Test]
    public void RectangularDefaultKeepsAllThreeGeometryRolesIdentical()
    {
        NodeGeometry Geometry = NodeGeometry.Rectangle(120, 60);
        Assert.Multiple(() =>
        {
            Assert.That(Geometry.ConnectionBoundary, Is.SameAs(Geometry.ObstacleGeometry));
            Assert.That(Geometry.ConnectionBoundary.TryPort("right", 0.5, out NodeGeometryPort Port), Is.True);
            Assert.That(Port, Is.EqualTo(new NodeGeometryPort(60, 0, 1, 0)));
            Assert.That(Geometry.ConnectionBoundary.Contains(Port.X, Port.Y, 0), Is.True);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(60, 0, 120, 0), Is.False);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(-120, 0, 120, 0), Is.True);
        });
    }

    /// <summary>Visible decoration is not automatically an eligible connection boundary.</summary>
    [Test]
    public void ConnectionBoundaryCanDifferFromTheVisibleExtent()
    {
        NodeGeometryBounds Visual = new NodeGeometryBounds(-100, -60, 100, 60);
        RectangularNodeGeometry Body = new RectangularNodeGeometry(new(-80, -40, 80, 60));
        NodeGeometry Geometry = new NodeGeometry(Visual, Body, Body);
        Assert.Multiple(() =>
        {
            Assert.That(Geometry.ConnectionBoundary.TryPort("right", 0.5, out NodeGeometryPort Port), Is.True);
            Assert.That(Port.X, Is.EqualTo(80));
            Assert.That(Port.X, Is.LessThan(Geometry.VisualBounds.Right));
            Assert.That(Geometry.ConnectionBoundary.Contains(100, 10, 0), Is.False);
        });
    }

    /// <summary>A corner touch or a boundary run does not become an interior intersection through rounding.</summary>
    [Test]
    public void ObstacleIntersectionDistinguishesBoundaryTouchesAndOccupiedInterior()
    {
        INodeObstacleGeometry Geometry = NodeGeometry.Rectangle(120, 60).ObstacleGeometry;
        Assert.Multiple(() =>
        {
            Assert.That(Geometry.IntersectsSegmentInterior(-120, -30, 120, -30), Is.False);
            Assert.That(Geometry.IntersectsSegmentInterior(-120, -90, -60, -30), Is.False);
            Assert.That(Geometry.IntersectsSegmentInterior(-120, -90, 0, 30), Is.True);
            Assert.That(Geometry.IntersectsSegmentInterior(0, 30, 0, 120), Is.False);
        });
    }

    /// <summary>Default geometry rejects invalid measured dimensions before routing can consume them.</summary>
    /// <param name="Width">The invalid width.</param>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidDimensionsAreRejected(double Width)
        => Assert.Throws<ArgumentOutOfRangeException>(() => NodeGeometry.Rectangle(Width, 60));
}
