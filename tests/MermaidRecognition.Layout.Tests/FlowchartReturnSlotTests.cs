using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that settling a return route preserves the other ports in its joint allocation.</summary>
[TestFixture]
public sealed class FlowchartReturnSlotTests
{
    /// <summary>A return from a decision keeps each lone slanted-edge port at the edge midpoint.</summary>
    [Test]
    public void ReturnFromDecisionKeepsTheFinalEdgeSlots()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
                A["Incoming message"] --> B["Determine next destination"] --> C["Send"] --> D{"More routing?"}
                D -- Yes --> B
                D -- No --> E["Complete"]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Model), Is.True);
        FlowchartLayoutNodeBounds Box = Model.NodeBounds.Single(Node => Node.NodeId == "D");
        FlowchartLayoutEdgeRoute Return = Model.EdgeRoutes.Single(Route => Route.SourceId == "D" && Route.TargetId == "B");
        double X = (Return.Points[0].X - Box.CenterX) / (Box.Width / 2);
        double Y = (Return.Points[0].Y - Box.CenterY) / (Box.Height / 2);
        Assert.That(Math.Abs(X), Is.EqualTo(0.5).Within(0.001));
        Assert.That(Math.Abs(Y), Is.EqualTo(0.5).Within(0.001));
    }
}
