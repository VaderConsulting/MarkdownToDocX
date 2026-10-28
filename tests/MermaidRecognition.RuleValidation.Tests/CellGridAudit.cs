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
        Dictionary<string, string> Result = new Dictionary<string, string>(StringComparer.Ordinal);
        FlowchartLogicalGrid.Snapshot State = FlowchartCellRepair.Capture(Nodes);
        FlowchartLogicalGridConfiguration Configuration = State.Owner.Configuration;
        bool GapsLegal = Configuration.ColumnGap >= FlowchartLayoutRules.BaseColumnGap
            && Configuration.RowGap >= FlowchartLayoutRules.BaseRowGap
            && State.ColumnGaps.Values.All(Gap => double.IsFinite(Gap) && Gap >= Configuration.ColumnGap)
            && State.RowGaps.Values.All(Gap => double.IsFinite(Gap) && Gap >= Configuration.RowGap);
        // Audit ownership independently of the production assignment validator.
        HashSet<string> Shared = new HashSet<string>(StringComparer.Ordinal);
        KeyValuePair<string, FlowchartCellRegion>[] Regions = [.. State.Regions];
        for (int First = 0; First < Regions.Length; First++)
        {
            for (int Second = First + 1; Second < Regions.Length; Second++)
        {
                FlowchartCellRegion A = Regions[First].Value; FlowchartCellRegion B = Regions[Second].Value;
            if (A.StartRow < (long)B.StartRow + B.RowSpan && B.StartRow < (long)A.StartRow + A.RowSpan
                && A.StartColumn < (long)B.StartColumn + B.ColumnSpan && B.StartColumn < (long)A.StartColumn + A.ColumnSpan)
            { Shared.Add(Regions[First].Key); Shared.Add(Regions[Second].Key); }
        }
        }

        foreach (FlowchartLayoutNodeBounds? Extra in Layout.NodeBounds.Where(Box => !State.Regions.ContainsKey(Box.NodeId)))
        {
            Result["node:" + Extra.NodeId] = "node";
        }

        foreach (var Extra in State.Regions.Keys.Where(Id => !Nodes.Any(Node => Node.Id == Id)))
        {
            Result["node:" + Extra] = "node";
        }

        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            if (!State.Regions.TryGetValue(Node.Id, out FlowchartCellRegion? Region))
            {
                Result["node:" + Node.Id] = "node";
                continue;
            }
            FlowchartLayoutNodeBounds Expected = Node.CellNode!.Bounds(Region, Node.GeometryProvider, State);
            FlowchartLayoutNodeBounds? Actual = Layout.NodeBounds.FirstOrDefault(Box => Box.NodeId == Node.Id);
            // Integer rectangles cannot represent fractional or disconnected occupancy.
            if (!GapsLegal || Shared.Contains(Node.Id) || !ReferenceEquals(Node.CellNode!.Grid, State.Owner)
                || Region.RowSpan <= 0 || Region.ColumnSpan <= 0
                || (long)Region.StartRow + Region.RowSpan != Region.EndRow
                || (long)Region.StartColumn + Region.ColumnSpan != Region.EndColumn
                || Layout.NodeBounds.Count(Box => Box.NodeId == Node.Id) != 1 || Actual == null
                || !Same(Expected.CenterX, Actual.CenterX) || !Same(Expected.CenterY, Actual.CenterY)
                || !Same(Expected.Width, Actual.Width) || !Same(Expected.Height, Actual.Height))
            {
                Result["node:" + Node.Id] = "node";
            }
        }
        DiagramGridLayout.GridFrame[] Frames = [.. DiagramGridLayout.ProjectCellFrames(Contract, [.. Nodes], State)];
        foreach (FlowchartLayoutNodeBounds? Extra in Layout.ClusterBounds.Where(Box => !Frames.Any(Frame => Frame.Id == Box.NodeId)))
        {
            Result[$"frame:{Extra.NodeId}:unexpected"] = "frame-edge";
        }

        foreach (DiagramGridLayout.GridFrame Frame in Frames)
        {
            FlowchartLayoutNodeBounds? Actual = Layout.ClusterBounds.FirstOrDefault(Box => Box.NodeId == Frame.Id);
            if (Layout.ClusterBounds.Count(Box => Box.NodeId == Frame.Id) != 1)
            {
                Result[$"frame:{Frame.Id}:ownership"] = "frame-edge";
            }

            if (Actual == null || !Same(Frame.Left, Actual.CenterX - (Actual.Width / 2)))
            {
                Result[$"frame:{Frame.Id}:left"] = "frame-edge";
            }

            if (Actual == null || !Same(Frame.Right, Actual.CenterX + (Actual.Width / 2)))
            {
                Result[$"frame:{Frame.Id}:right"] = "frame-edge";
            }

            if (Actual == null || !Same(Math.Min(Frame.Top, Frame.Bottom), Actual.CenterY - (Actual.Height / 2)))
            {
                Result[$"frame:{Frame.Id}:bottom"] = "frame-edge";
            }

            if (Actual == null || !Same(Math.Max(Frame.Top, Frame.Bottom), Actual.CenterY + (Actual.Height / 2)))
            {
                Result[$"frame:{Frame.Id}:top"] = "frame-edge";
            }

            if (Result.Keys.Any(Key => Key.StartsWith("frame:" + Frame.Id + ":", StringComparison.Ordinal)))
            {
                NUnit.Framework.TestContext.Out.WriteLine($"CELL_GRID_FRAME|{NUnit.Framework.TestContext.CurrentContext.Test.Name}|{Frame.Id}|"
                    + System.Text.Json.JsonSerializer.Serialize(new { Expected = new { Frame.Left, Frame.Right, Frame.Top, Frame.Bottom },
                        Actual = Actual == null ? null : new { Left = Actual.CenterX - (Actual.Width / 2), Right = Actual.CenterX + (Actual.Width / 2),
                            Top = Actual.CenterY + (Actual.Height / 2), Bottom = Actual.CenterY - (Actual.Height / 2) } }));
            }
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
