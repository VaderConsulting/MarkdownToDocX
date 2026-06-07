namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class NativeFlowchartRenderBridgeTests
{
    [Test]
    public void TryRenderFlowchartSvg_Chain_Fixture_Returns_Svg()
    {
        string MmdPath = Path.Combine(FindFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        string Source = File.ReadAllText(MmdPath);

        Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Source, out string Svg), Is.True);
        Assert.That(Svg, Does.StartWith("<svg"));
    }

    [Test]
    public void TryRenderFlowchartSvg_Sequence_Returns_False()
    {
        Assert.That(
            NativeFlowchartRenderBridge.TryRenderFlowchartSvg("sequenceDiagram\n    A->>B: hi", out string Svg),
            Is.False);
        Assert.That(Svg, Is.EqualTo(string.Empty));
    }

    private static string FindFlowchartFixtureDirectory()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, "FlowchartRecognition");
            if (Directory.Exists(Candidate))
            {
                return Candidate;
            }

            Current = Current.Parent;
        }

        throw new InvalidOperationException("Could not locate FlowchartRecognition fixtures.");
    }
}
