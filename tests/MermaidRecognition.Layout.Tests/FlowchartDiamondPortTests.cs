using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Diamond ports: vertices first, then a minimum three-port lattice on each edge.
/// </summary>
[TestFixture]
public sealed class FlowchartDiamondPortTests
{
    private const int WIDTH_PX = 150;

    private const int HEIGHT_PX = 60;

    [Test]
    public void Vertices_AreTheFourTipsOfTheStandardCell()
    {
        IReadOnlyList<(int X, int Y)> Ports = FlowchartDiamondPorts.Vertices(WIDTH_PX, HEIGHT_PX);

        Assert.That(Ports, Is.EqualTo(new[] { (0, 30), (75, 0), (0, -30), (-75, 0) }));
    }

    [Test]
    public void EdgePorts_OnePort_SitsAtTheRoundedMidpoint()
    {
        bool Found = FlowchartDiamondPorts.TryGetEdgePorts(
            WIDTH_PX,
            HEIGHT_PX,
            FlowchartDiamondEdge.TopRight,
            1,
            out IReadOnlyList<(int X, int Y)> Ports);

        Assert.That(Found, Is.True);
        Assert.That(Ports, Is.EqualTo(new[] { (38, 15) }));
    }

    [Test]
    public void EdgePorts_TwoPorts_SitAtTheThirdPoints()
    {
        bool Found = FlowchartDiamondPorts.TryGetEdgePorts(
            WIDTH_PX,
            HEIGHT_PX,
            FlowchartDiamondEdge.TopRight,
            2,
            out IReadOnlyList<(int X, int Y)> Ports);

        Assert.That(Found, Is.True);
        Assert.That(Ports, Is.EqualTo(new[] { (25, 20), (50, 10) }));
    }

    [Test]
    public void EdgePorts_ThreePorts_SitAtTheQuarterPoints()
    {
        bool Found = FlowchartDiamondPorts.TryGetEdgePorts(
            WIDTH_PX,
            HEIGHT_PX,
            FlowchartDiamondEdge.TopRight,
            3,
            out IReadOnlyList<(int X, int Y)> Ports);

        Assert.That(Found, Is.True);
        Assert.That(Ports, Is.EqualTo(new[] { (19, 23), (38, 15), (56, 8) }));
    }

    [Test]
    public void EdgePorts_FourPorts_SitAtTheFifthPoints()
    {
        bool Found = FlowchartDiamondPorts.TryGetEdgePorts(
            WIDTH_PX,
            HEIGHT_PX,
            FlowchartDiamondEdge.TopRight,
            4,
            out IReadOnlyList<(int X, int Y)> Ports);

        Assert.That(Found, Is.True);
        Assert.That(Ports, Is.EqualTo(new[] { (15, 24), (30, 18), (45, 12), (60, 6) }));
    }

    [Test]
    public void EdgePorts_OppositeEdge_MirrorsAboutTheCentre()
    {
        bool TopFound = FlowchartDiamondPorts.TryGetEdgePorts(
            WIDTH_PX,
            HEIGHT_PX,
            FlowchartDiamondEdge.TopRight,
            3,
            out IReadOnlyList<(int X, int Y)> TopRight);
        bool BottomFound = FlowchartDiamondPorts.TryGetEdgePorts(
            WIDTH_PX,
            HEIGHT_PX,
            FlowchartDiamondEdge.BottomLeft,
            3,
            out IReadOnlyList<(int X, int Y)> BottomLeft);

        Assert.That(TopFound, Is.True);
        Assert.That(BottomFound, Is.True);
        Assert.That(BottomLeft[0], Is.EqualTo((-TopRight[0].X, -TopRight[0].Y)));
        Assert.That(BottomLeft[1], Is.EqualTo((-TopRight[1].X, -TopRight[1].Y)));
        Assert.That(BottomLeft[2], Is.EqualTo((-TopRight[2].X, -TopRight[2].Y)));
    }

    [Test]
    public void EdgePorts_EvenCell_DoesNotMoveAnExactFraction()
    {
        bool Found = FlowchartDiamondPorts.TryGetEdgePorts(
            160,
            80,
            FlowchartDiamondEdge.TopRight,
            3,
            out IReadOnlyList<(int X, int Y)> Ports);

        Assert.That(Found, Is.True);
        Assert.That(Ports, Is.EqualTo(new[] { (20, 30), (40, 20), (60, 10) }));
    }

    [Test]
    public void TryAssign_CardinalLines_UseOnlyVertices()
    {
        List<(double X, double Y)> Destinations =
        [
            (0.0, 100.0),
            (100.0, 0.0),
            (0.0, -100.0),
            (-100.0, 0.0)
        ];

        bool Assigned = FlowchartDiamondPorts.TryAssign(
            WIDTH_PX,
            HEIGHT_PX,
            Destinations,
            out IReadOnlyList<FlowchartDiamondPort> Ports);

        Assert.That(Assigned, Is.True);
        Assert.That(Ports[0].IsVertex, Is.True);
        Assert.That(Ports[0].OffsetX, Is.EqualTo(0));
        Assert.That(Ports[0].OffsetY, Is.EqualTo(30));
        Assert.That(Ports[0].DepartureX, Is.EqualTo(0));
        Assert.That(Ports[0].DepartureY, Is.EqualTo(1));
        Assert.That(Ports[1].OffsetX, Is.EqualTo(75));
        Assert.That(Ports[1].DepartureX, Is.EqualTo(1));
        Assert.That(Ports[1].DepartureY, Is.EqualTo(0));
        Assert.That(Ports[2].OffsetY, Is.EqualTo(-30));
        Assert.That(Ports[3].OffsetX, Is.EqualTo(-75));
        foreach (FlowchartDiamondPort Port in Ports)
        {
            Assert.That(Port.IsVertex, Is.True);
            Assert.That(Math.Abs(Port.DepartureX) + Math.Abs(Port.DepartureY), Is.EqualTo(1));
        }
    }

    [Test]
    public void TryAssign_SecondLineOnTheSameSide_UsesTheAdjacentFreeVertexFirst()
    {
        List<(double X, double Y)> Destinations =
        [
            (0.0, 100.0),
            (40.0, 90.0)
        ];

        bool Assigned = FlowchartDiamondPorts.TryAssign(
            WIDTH_PX,
            HEIGHT_PX,
            Destinations,
            out IReadOnlyList<FlowchartDiamondPort> Ports);

        Assert.That(Assigned, Is.True);
        Assert.That(Ports[0].IsVertex, Is.True);
        Assert.That(Ports[0].OffsetY, Is.EqualTo(30));
        Assert.That(Ports[1].IsVertex, Is.True);
        Assert.That(Ports[1].OffsetX, Is.EqualTo(75));
        Assert.That(Ports[1].OffsetY, Is.EqualTo(0));
        Assert.That(Ports[1].DepartureX, Is.EqualTo(1));
        Assert.That(Ports[1].DepartureY, Is.EqualTo(0));
    }

    [Test]
    public void TryAssign_ThreeLinesOnOneEdge_UseQuarterPointsInOrder()
    {
        List<(double X, double Y)> Destinations =
        [
            (0.0, 200.0),
            (200.0, 0.0),
            (30.0, 80.0),
            (80.0, 30.0),
            (50.0, 50.0)
        ];

        bool Assigned = FlowchartDiamondPorts.TryAssign(
            WIDTH_PX,
            HEIGHT_PX,
            Destinations,
            out IReadOnlyList<FlowchartDiamondPort> Ports);

        Assert.That(Assigned, Is.True);
        Assert.That(Ports[0].IsVertex, Is.True);
        Assert.That(Ports[1].IsVertex, Is.True);
        Assert.That(Ports[2].OffsetX, Is.EqualTo(19));
        Assert.That(Ports[2].OffsetY, Is.EqualTo(23));
        Assert.That(Ports[2].DepartureY, Is.EqualTo(1));
        Assert.That(Ports[4].OffsetX, Is.EqualTo(38));
        Assert.That(Ports[4].OffsetY, Is.EqualTo(15));
        Assert.That(Ports[3].OffsetX, Is.EqualTo(56));
        Assert.That(Ports[3].OffsetY, Is.EqualTo(8));
        Assert.That(Ports[3].DepartureX, Is.EqualTo(1));
        Assert.That(Ports[3].DepartureY, Is.EqualTo(0));
    }

    [Test]
    public void TryAssign_MoreThanFourLinesOnOneEdge_RedistributesTheActivePortsEvenly()
    {
        List<(double X, double Y)> Destinations =
        [
            (0.0, 300.0),
            (300.0, 0.0),
            (15.0, 120.0),
            (35.0, 100.0),
            (55.0, 80.0),
            (80.0, 55.0),
            (100.0, 35.0),
            (120.0, 15.0)
        ];

        bool Assigned = FlowchartDiamondPorts.TryAssign(
            WIDTH_PX,
            HEIGHT_PX,
            Destinations,
            out IReadOnlyList<FlowchartDiamondPort> Ports);

        Assert.That(Assigned, Is.True);
        Assert.That(Ports.Skip(2).Select(Port => (Port.OffsetX, Port.OffsetY)).Distinct().Count(), Is.EqualTo(6));
        Assert.That(Ports.Skip(2).All(Port => Port.Edge == FlowchartDiamondEdge.TopRight), Is.True);
        Assert.That(Ports.Skip(2).Select(Port => Port.OffsetX), Is.Ordered.Ascending,
            "adding ports must not reverse two destinations and create a local crossing");
        Assert.That(FlowchartDiamondPorts.TryGetEdgePorts(WIDTH_PX, HEIGHT_PX,
            FlowchartDiamondEdge.TopRight, 6, out IReadOnlyList<(int X, int Y)> Available), Is.True);
        Assert.That(Available, Is.EqualTo(new[]
        {
            (11, 26), (21, 21), (32, 17), (43, 13), (54, 9), (64, 4)
        }));
    }

    [Test]
    public void TryAssign_EdgePorts_MinimiseDistanceFromRayIntersectionWithoutChangingOrder()
    {
        List<(double X, double Y)> Destinations =
        [
            (0.0, 300.0),
            (300.0, 0.0),
            (40.0, 60.0),
            (150.0, 20.0)
        ];

        bool Assigned = FlowchartDiamondPorts.TryAssign(
            WIDTH_PX,
            HEIGHT_PX,
            Destinations,
            out IReadOnlyList<FlowchartDiamondPort> Ports);

        Assert.That(Assigned, Is.True);
        Assert.That((Ports[2].OffsetX, Ports[2].OffsetY), Is.EqualTo((19, 23)));
        Assert.That((Ports[3].OffsetX, Ports[3].OffsetY), Is.EqualTo((56, 8)));
    }
}
