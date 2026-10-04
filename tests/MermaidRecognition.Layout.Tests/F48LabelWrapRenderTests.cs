using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F48LabelWrapRenderTests
{
    const string F48 = """
        flowchart TB
        A["Symbols: star#42; underscore#95; backtick#96; brackets#91;#93; pipe A#124;B"] --> B["Quotes: double#quot; and apostrophe#39;s"]
        B --> C["Comparison: a#lt; b #38;#38; c#gt; d"]
        """;

    [Test]
    public void RenderToSvg_F48_DecodedSymbols_UseWrappedMultilineLabels()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F48);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);

        FlowchartNodeDimensions NodeA = FlowchartNodeSizeEstimator.Estimate(
            Contract.Nodes.Single(Node => Node.Id == "A"),
            Options);
        Assert.That(NodeA.DisplayLabel, Does.Contain("\n"));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Regex.Matches(Svg, "<tspan").Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(Svg, Does.Contain("star*"));
        Assert.That(Svg, Does.Contain("double&quot;"));
        Assert.That(Svg, Does.Not.Contain("#quot;"));
    }
}
