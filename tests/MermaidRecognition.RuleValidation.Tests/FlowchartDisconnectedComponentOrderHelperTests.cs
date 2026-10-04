using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.RuleValidation.Generation;
using MermaidRecognition.RuleValidation.Models;

using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>
/// Disconnected component flow-axis ordering validation tests.
/// </summary>
[TestFixture]
public sealed class FlowchartDisconnectedComponentOrderHelperTests
{
    [Test]
    public void ApplyDisconnectedComponentOrderFacts_TdVerticalStack_IsValid()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[One]
            B[Two]
            C[Three]
            """);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal)
        {
            ["A"] = new FlowchartLayoutNodeBounds { NodeId = "A", CenterX = 100.0, CenterY = 300.0, Width = 80.0, Height = 40.0 },
            ["B"] = new FlowchartLayoutNodeBounds { NodeId = "B", CenterX = 100.0, CenterY = 200.0, Width = 80.0, Height = 40.0 },
            ["C"] = new FlowchartLayoutNodeBounds { NodeId = "C", CenterX = 100.0, CenterY = 100.0, Width = 80.0, Height = 40.0 }
        };

        DiagramActualResult Diagram = new DiagramActualResult { DiagramId = "F82" };
        FlowchartDisconnectedComponentOrderHelper.ApplyDisconnectedComponentOrderFacts(
            Diagram,
            Contract,
            BoundsById,
            FlowchartDirection.TopToBottom);

        Assert.That(Diagram.Layout["HasDisconnectedSingleNodeComponents"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["DisconnectedComponentsFollowFlowAxis"], Is.EqualTo(true));
    }

    [Test]
    public void ApplyDisconnectedComponentOrderFacts_TdHorizontalRow_IsInvalid()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[One]
            B[Two]
            C[Three]
            """);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal)
        {
            ["A"] = new FlowchartLayoutNodeBounds { NodeId = "A", CenterX = 100.0, CenterY = 100.0, Width = 80.0, Height = 40.0 },
            ["B"] = new FlowchartLayoutNodeBounds { NodeId = "B", CenterX = 250.0, CenterY = 100.0, Width = 80.0, Height = 40.0 },
            ["C"] = new FlowchartLayoutNodeBounds { NodeId = "C", CenterX = 400.0, CenterY = 100.0, Width = 80.0, Height = 40.0 }
        };

        DiagramActualResult Diagram = new DiagramActualResult { DiagramId = "F82" };
        FlowchartDisconnectedComponentOrderHelper.ApplyDisconnectedComponentOrderFacts(
            Diagram,
            Contract,
            BoundsById,
            FlowchartDirection.TopToBottom);

        Assert.That(Diagram.Layout["HasDisconnectedSingleNodeComponents"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["DisconnectedComponentsFollowFlowAxis"], Is.EqualTo(false));
    }

    [Test]
    public void ApplyDisconnectedComponentOrderFacts_LrHorizontalRow_IsValid()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[One]
            B[Two]
            C[Three]
            """);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal)
        {
            ["A"] = new FlowchartLayoutNodeBounds { NodeId = "A", CenterX = 100.0, CenterY = 100.0, Width = 80.0, Height = 40.0 },
            ["B"] = new FlowchartLayoutNodeBounds { NodeId = "B", CenterX = 250.0, CenterY = 100.0, Width = 80.0, Height = 40.0 },
            ["C"] = new FlowchartLayoutNodeBounds { NodeId = "C", CenterX = 400.0, CenterY = 100.0, Width = 80.0, Height = 40.0 }
        };

        DiagramActualResult Diagram = new DiagramActualResult { DiagramId = "F12" };
        FlowchartDisconnectedComponentOrderHelper.ApplyDisconnectedComponentOrderFacts(
            Diagram,
            Contract,
            BoundsById,
            FlowchartDirection.LeftToRight);

        Assert.That(Diagram.Layout["HasDisconnectedSingleNodeComponents"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["DisconnectedComponentsFollowFlowAxis"], Is.EqualTo(true));
    }
}
