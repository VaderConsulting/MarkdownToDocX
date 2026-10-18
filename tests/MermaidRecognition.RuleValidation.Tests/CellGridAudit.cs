using MermaidRecognition.Contract;
using MermaidRecognition.Layout;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Audits logical-grid ownership and forward projections, rather than applying the legacy centre lattice to whole-cell spans.</summary>
internal static class CellGridAudit
{
    /// <summary>Checks complete exclusive regions and exact derived node/frame bounds against the returned model.</summary>
    /// <param name="Contract">Declared memberships and frame geometry.</param>
    /// <param name="Layout">Final output whose cached bounds must agree with authoritative regions.</param>
    /// <param name="Nodes">Actual bound engine nodes retained by the experimental pipeline.</param>
    /// <returns>Node or frame-edge faults using the existing audit categories.</returns>
    internal static Dictionary<string, string> Faults(DiagramContract Contract, FlowchartLayoutResult Layout,
        IReadOnlyList<DiagramGridLayout.GridNode> Nodes)
    {
        var Result = new Dictionary<string, string>(StringComparer.Ordinal);
        var State = FlowchartCellRepair.Capture(Nodes);
        bool Exclusive = Nodes[0].CellNode!.Grid.TryPrepare(State.Regions, [], out _);
        foreach (var Node in Nodes)
        {
            var Region = State.Regions[Node.Id];
            var Expected = Node.CellNode!.Bounds(Region, Node.GeometryProvider, State);
            var Actual = Layout.NodeBounds.FirstOrDefault(Box => Box.NodeId == Node.Id);
            if (!Exclusive || Region.RowSpan <= 0 || Region.ColumnSpan <= 0 || Actual == null
                || !Same(Expected.CenterX, Actual.CenterX) || !Same(Expected.CenterY, Actual.CenterY)
                || !Same(Expected.Width, Actual.Width) || !Same(Expected.Height, Actual.Height))
                Result["node:" + Node.Id] = "node";
        }
        foreach (var Frame in DiagramGridLayout.ProjectCellFrames(Contract, Nodes.ToList(), State))
        {
            var Actual = Layout.ClusterBounds.FirstOrDefault(Box => Box.NodeId == Frame.Id);
            if (Actual == null || !Same(Frame.Left, Actual.CenterX - Actual.Width / 2)) Result[$"frame:{Frame.Id}:left"] = "frame-edge";
            if (Actual == null || !Same(Frame.Right, Actual.CenterX + Actual.Width / 2)) Result[$"frame:{Frame.Id}:right"] = "frame-edge";
            if (Actual == null || !Same(Math.Min(Frame.Top, Frame.Bottom), Actual.CenterY - Actual.Height / 2)) Result[$"frame:{Frame.Id}:bottom"] = "frame-edge";
            if (Actual == null || !Same(Math.Max(Frame.Top, Frame.Bottom), Actual.CenterY + Actual.Height / 2)) Result[$"frame:{Frame.Id}:top"] = "frame-edge";
            if (Result.Keys.Any(Key => Key.StartsWith("frame:" + Frame.Id + ":", StringComparison.Ordinal)))
                NUnit.Framework.TestContext.Out.WriteLine($"CELL_GRID_FRAME|{NUnit.Framework.TestContext.CurrentContext.Test.Name}|{Frame.Id}|"
                    + System.Text.Json.JsonSerializer.Serialize(new { Expected = new { Frame.Left, Frame.Right, Frame.Top, Frame.Bottom },
                        Actual = Actual == null ? null : new { Left = Actual.CenterX - Actual.Width / 2, Right = Actual.CenterX + Actual.Width / 2,
                            Top = Actual.CenterY + Actual.Height / 2, Bottom = Actual.CenterY - Actual.Height / 2 } }));
        }
        return Result;
    }

    /// <summary>Uses the audit's existing geometric tolerance while rejecting nonfinite output bounds.</summary>
    /// <param name="Expected">Forward-derived coordinate or extent.</param>
    /// <param name="Actual">Returned coordinate or extent.</param>
    /// <returns>Whether both finite values agree within the established audit tolerance.</returns>
    private static bool Same(double Expected, double Actual) => double.IsFinite(Expected) && double.IsFinite(Actual)
        && Math.Abs(Expected - Actual) <= 0.001;
}
