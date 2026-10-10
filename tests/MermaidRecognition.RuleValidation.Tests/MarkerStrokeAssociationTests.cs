using MermaidRecognition.Core;
using MermaidRecognition.RuleValidation.Generation;
using MermaidRecognition.RuleValidation.Models;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture]
public sealed class MarkerStrokeAssociationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void MarkerFacts_UseTheTaggedTerminalStrokeAndRejectMisalignment(bool Misaligned)
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA --> B\nA --> C");
        DiagramActualResult Diagram = new()
        {
            LayoutSucceeded = true,
            Edges = [new() { SourceNodeId = "A", TargetNodeId = "B" },
                     new() { SourceNodeId = "A", TargetNodeId = "C" }]
        };
        string FirstArrow = Misaligned ? "100,0 96,-10 104,-10" : "100,0 90,3 90,-3";
        string Svg = """
            <svg>
            <path d="M0,20L100,20" fill="none" stroke="#333" data-edge-source="A" data-edge-target="C"/>
            <line x1="0" y1="0" x2="40" y2="0" stroke-linecap="butt" data-edge-source="A" data-edge-target="B"/>
            <line x1="60" y1="0" x2="90" y2="0" stroke-linecap="butt" data-edge-source="A" data-edge-target="B"/>
            """
            + $"<polygon points=\"{FirstArrow}\" fill=\"#333\" stroke=\"none\"/>"
            + "<polygon points=\"100,20 90,23 90,17\" fill=\"#333\" stroke=\"none\"/></svg>";

        FlowchartSvgMarkerTangentFactsHelper.Apply(Diagram, Contract, Svg);

        Assert.That(Diagram.Edges[0].MarkerFacts["EndAlignedWithPathTangent"], Is.EqualTo(!Misaligned));
        Assert.That(Diagram.Edges[1].MarkerFacts["EndAlignedWithPathTangent"], Is.EqualTo(true));
    }
}
