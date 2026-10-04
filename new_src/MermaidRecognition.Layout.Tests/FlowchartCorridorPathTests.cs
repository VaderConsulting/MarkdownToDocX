using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartCorridorPathTests
{
    [Test]
    public void BuildCorridorRoutePathData_UsesCurveBasisNotLinearPolyline()
    {
        List<(double X, double Y)> Points =
        [
            (10.0, 100.0),
            (10.0, 90.0),
            (10.0, 50.0),
            (80.0, 50.0),
            (80.0, 40.0),
            (80.0, 30.0)
        ];

        string PathData = FlowchartCurvePathBuilder.BuildCorridorRoutePathData(Points);

        Assert.That(PathData, Does.Contain("C"));
        Assert.That(
            FlowchartCurvePathBuilder.IsMultiSegmentOrthogonalPathData(PathData),
            Is.False,
            "Corridor routes must not remain pure orthogonal polylines under curveBasis");
    }
}
