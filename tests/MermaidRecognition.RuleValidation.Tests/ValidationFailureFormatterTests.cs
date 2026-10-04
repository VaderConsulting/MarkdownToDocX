using MermaidRecognition.RuleValidation.Engine;
using MermaidRecognition.RuleValidation.Models;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>
/// Human-readable validation failure formatting tests.
/// </summary>
[TestFixture]
public sealed class ValidationFailureFormatterTests
{
    [Test]
    public void DescribeEdge_UsesVisibleNodeLabelsAndEdgeLabel()
    {
        DiagramActualResult Diagram = BuildDiagram();
        EdgeActualResult Edge = Diagram.Edges[0];

        string Description = ValidationFailureFormatter.DescribeEdge(Diagram, Edge);

        Assert.That(Description, Is.EqualTo("\"Merge?\" → \"Continue\" (yes)"));
    }

    [Test]
    public void BuildSuggestedCorrection_NodeOverlap_IncludesNodeLabelsCentresAndSeparationAxis()
    {
        EvaluationContext Context = BuildNodePairContext();
        string Correction = ValidationFailureFormatter.BuildSuggestedCorrection(
            "Layout.Node.NoOverlap",
            "Layout.NodeBodiesDoNotOverlap",
            Context,
            "\"Receive\"",
            "Adjust layout so unrelated node bodies do not overlap beyond tolerance.");

        Assert.That(Correction, Does.Contain("F34"));
        Assert.That(Correction, Does.Contain("Receive"));
        Assert.That(Correction, Does.Contain("Done"));
        Assert.That(Correction, Does.Contain("horizontal lane spacing"));
        Assert.That(Correction, Does.Contain("rank"));
        Assert.That(Correction, Does.Not.Contain("Adjust layout so unrelated node bodies do not overlap beyond tolerance."));
    }

    [Test]
    public void BuildSuggestedCorrection_EdgeHook_IncludesArrowDirectionAndDiagnosticReason()
    {
        EvaluationContext Context = BuildEdgeContext();
        Context.Diagram.Direction = "LR";
        Context.Edge!.Label = "No";
        Context.Edge.SourceNodeId = "B";
        Context.Edge.TargetNodeId = "D";
        Context.Edge.Facts["Diagnostic.Reason"] = "Route reverses direction near the source attach, producing a local hook or overshoot.";
        Context.Edge.Path.Points.Add(new PointActualResult { X = 1, Y = 2 });
        Context.Edge.Path.Points.Add(new PointActualResult { X = 3, Y = 4 });

        string Correction = ValidationFailureFormatter.BuildSuggestedCorrection(
            "Routing.Path.NoHookAtSource",
            "Path.NoHookAtSource",
            Context,
            "\"OK?\" → \"Retry\" (No)",
            "Adjust the route path to satisfy the required path-shape constraint.");

        Assert.That(Correction, Does.Contain("OK?"));
        Assert.That(Correction, Does.Contain("Retry"));
        Assert.That(Correction, Does.Contain("left-to-right"));
        Assert.That(Correction, Does.Contain("hook or overshoot"));
        Assert.That(Correction, Does.Contain("guide points"));
    }

    [Test]
    public void BuildMessage_ReplacesInternalEdgeIdWithHumanReadableArrowDescription()
    {
        EvaluationContext Context = BuildEdgeContext();
        string Message = ValidationFailureFormatter.BuildMessage(
            "Edge {EdgeId} failed Routing.Path.NoUnnecessaryDetour.",
            "Routing.Path.NoUnnecessaryDetour",
            Context,
            "Path.NoUnnecessaryDetour",
            ValidationFailureFormatter.DescribeElement(Context));

        Assert.That(Message, Does.Contain("F35"));
        Assert.That(Message, Does.Not.Contain("F35 — F35"));
        Assert.That(Message, Does.Contain("Path B"));
        Assert.That(Message, Does.Contain("Merge?"));
        Assert.That(Message, Does.Not.Contain("L-B-D-1"));
        Assert.That(Message, Does.Contain("unnecessary detour"));
    }

    private static DiagramActualResult BuildDiagram()
    {
        DiagramActualResult Diagram = new DiagramActualResult
        {
            DiagramId = "F35",
            ScenarioTitle = "### F35 — Diamond with multiple incoming edges"
        };
        Diagram.Nodes.Add(new NodeActualResult { NodeId = "B", Label = "Path B" });
        Diagram.Nodes.Add(new NodeActualResult { NodeId = "D", Label = "Merge?" });
        Diagram.Nodes.Add(new NodeActualResult { NodeId = "E", Label = "Continue" });
        Diagram.Edges.Add(new EdgeActualResult
        {
            EdgeId = "L-D-E-3",
            SourceNodeId = "D",
            TargetNodeId = "E",
            Label = "yes"
        });
        return Diagram;
    }

    private static EvaluationContext BuildEdgeContext()
    {
        DiagramActualResult Diagram = BuildDiagram();
        Diagram.Nodes.Add(new NodeActualResult { NodeId = "B", Label = "OK?" });
        Diagram.Nodes.Add(new NodeActualResult { NodeId = "D", Label = "Retry" });
        EdgeActualResult Edge = new EdgeActualResult
        {
            EdgeId = "L-B-D-1",
            SourceNodeId = "B",
            TargetNodeId = "D"
        };
        Diagram.Edges.Add(Edge);

        return new EvaluationContext
        {
            Diagram = Diagram,
            Edge = Edge,
            ElementType = "Edge",
            ElementId = Edge.EdgeId
        };
    }

    private static EvaluationContext BuildNodePairContext()
    {
        DiagramActualResult Diagram = new DiagramActualResult
        {
            DiagramId = "F34",
            Direction = "LR",
            ScenarioTitle = "### F34 — Simple cycle (retry loop)"
        };
        NodeActualResult Receive = new NodeActualResult
        {
            NodeId = "A",
            Label = "Receive",
            Rank = 0,
            Lane = 0,
            Bounds = new BoundsActualResult { CentreX = 580, CentreY = 60, X = 516.4, Y = 25, Width = 127.2, Height = 70 }
        };
        NodeActualResult Done = new NodeActualResult
        {
            NodeId = "C",
            Label = "Done",
            Rank = 1,
            Lane = 0,
            Bounds = new BoundsActualResult { CentreX = 673.6, CentreY = 0, X = 610, Y = -35, Width = 127.2, Height = 70 }
        };
        Diagram.Nodes.Add(Receive);
        Diagram.Nodes.Add(Done);

        EvaluationContext Context = new EvaluationContext
        {
            Diagram = Diagram,
            Node = Receive,
            ElementType = "NodePair",
            ElementId = "A+C"
        };
        Context.FieldValues["OtherNodeId"] = "C";
        Context.FieldValues["OtherNodeLabel"] = "Done";
        Context.FieldValues["Layout.NodeBodiesDoNotOverlap"] = false;
        return Context;
    }
}
