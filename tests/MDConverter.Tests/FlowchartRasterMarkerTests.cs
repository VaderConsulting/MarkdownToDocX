using System.Reflection;
using MDConverter.Pipeline;

namespace MDConverter.Tests;

[TestClass]
public sealed class FlowchartRasterMarkerTests
{
    [TestMethod]
    public void ExpandSvgFlowchartMarkers_ExtendsConnectorToTargetBoundary()
    {
        string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string Svg = File.ReadAllText(Path.Combine(RepoRoot, "out", "f15-svg", "000.naiad.svg"));

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;
        StringAssert.Contains(Expanded, "L220.72,24");
        StringAssert.Contains(Expanded, "M220.72,24");
        Assert.IsFalse(Expanded.Contains("marker-end", StringComparison.Ordinal));

        int TargetShapeIndex = Expanded.IndexOf("M220.72,0 H293.81 V48", StringComparison.Ordinal);
        int ExtendedConnectorIndex = Expanded.IndexOf("M170.72,24L220.72,24", StringComparison.Ordinal);
        Assert.IsTrue(TargetShapeIndex >= 0);
        Assert.IsTrue(ExtendedConnectorIndex > TargetShapeIndex);
    }
}
