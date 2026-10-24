using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Reflows root containers on their parent cell grid without local pixel layouts or copying coordinates back.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Plans complete root-container packings and retains an improving assignment only after all frame and ownership checks.</summary>
    /// <param name="Contract">Nested ownership and explicit hints.</param>
    /// <param name="Placed">Every node on the parent grid.</param>
    /// <param name="Frames">Frame cache rebuilt only for the accepted ownership.</param>
    /// <returns>Whether a complete improving plan was accepted.</returns>
    internal static bool PackCellRootGroups(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var OriginalFrames = Frames.ToArray();
        var Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        var Units = Placed.GroupBy(Node => CellRootUnit(Node, Groups), StringComparer.Ordinal).Select(Group => Group.ToArray()).ToArray();
        if (Units.Length < 2) return false;
        var Compact = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        foreach (var Unit in Units.Where(Unit => !Unit.Any(Node => Node.PlacementHintLocked)))
        {
            CompressCellRoot(Unit, false, Compact); CompressCellRoot(Unit, true, Compact);
        }
        int BestConflicts = CellFrameConflicts(Contract, Placed, FramesForCells(Contract, Placed), DiagramGridMetrics.GRID_UNIT_PX).Count;
        double BestScore = double.PositiveInfinity;
        FlowchartLogicalGrid.Snapshot? Best = null;
        int Candidates = 0;
        for (int Padding = 0; Padding <= FlowchartLayoutRules.RepairChainDepth; Padding++)
        for (int PerRow = 1; PerRow <= Units.Length && Candidates < FlowchartLayoutRules.RepairCandidateMoves; PerRow++)
        {
            Candidates++;
            var Targets = new Dictionary<string, FlowchartCellRegion>(Compact, StringComparer.Ordinal);
            int Row = 0, Column = 0, Height = 0;
            for (int Index = 0; Index < Units.Length; Index++)
            {
                if (Index > 0 && Index % PerRow == 0) { Row = checked(Row + Height + Padding); Column = 0; Height = 0; }
                var Unit = Units[Index];
                int Top = Unit.Min(Node => Compact[Node.Id].StartRow), Left = Unit.Min(Node => Compact[Node.Id].StartColumn);
                int Rows = Unit.Max(Node => Compact[Node.Id].EndRow) - Top, Columns = Unit.Max(Node => Compact[Node.Id].EndColumn) - Left;
                foreach (var Node in Unit)
                {
                    var Region = Compact[Node.Id];
                    if (!Unit.Any(Member => Member.PlacementHintLocked)) Targets[Node.Id] = new(
                        checked(Region.StartRow + Row - Top), checked(Region.StartColumn + Column - Left), Region.RowSpan, Region.ColumnSpan);
                }
                Column = checked(Column + Columns + Padding); Height = Math.Max(Height, Rows);
            }
            var Plan = FlowchartCellRepair.PlanAssignment(Placed, Targets);
            if (Plan?.TryApply() != true) continue;
            try
            {
                var TrialFrames = FramesForCells(Contract, Placed);
                int Conflicts = CellFrameConflicts(Contract, Placed, TrialFrames, DiagramGridMetrics.GRID_UNIT_PX).Count;
                double Width = Placed.Max(Node => Node.X + Node.Width / 2d) - Placed.Min(Node => Node.X - Node.Width / 2d);
                double HeightPx = Placed.Max(Node => Node.Y + Node.Height / 2d) - Placed.Min(Node => Node.Y - Node.Height / 2d);
                double Score = FlowchartLayoutScore.Calculate(Width, HeightPx, 0, 0);
                if (Conflicts < BestConflicts || Conflicts == BestConflicts && Best != null && Score < BestScore)
                {
                    BestConflicts = Conflicts; BestScore = Score; Best = FlowchartCellRepair.Capture(Placed);
                }
            }
            finally { Plan.Rollback(); }
        }
        if (Best == null) return false;
        // Publish once after every rejected trial has restored the exact parent
        // regions and gaps. Frames are a derived cache of this accepted state.
        FlowchartCellRepair.Restore(Placed, Best);
        try { ReplaceCellFrames(Contract, Placed, Frames); }
        catch
        {
            FlowchartCellRepair.Restore(Placed, Before); Frames.Clear(); Frames.AddRange(OriginalFrames); throw;
        }
        return true;
    }

    /// <summary>Finds a node's top-level container, treating an ungrouped node as its own unit.</summary>
    /// <param name="Node">Member whose declared ancestry is followed.</param>
    /// <param name="Groups">Declared ancestry lookup.</param>
    /// <returns>Stable unit identity.</returns>
    private static string CellRootUnit(GridNode Node, IReadOnlyDictionary<string, GroupContract> Groups)
        {
            string? Current = Node.GroupId;
            var Seen = new HashSet<string>(StringComparer.Ordinal);
            while (Current != null && Seen.Add(Current) && Groups.TryGetValue(Current, out var Group) && Group.ParentGroupId != null)
                Current = Group.ParentGroupId;
            return Current == null ? "node:" + Node.Id : "group:" + Current;
        }

    /// <summary>Removes only wholly empty logical strips inside one private unhinted container plan.</summary>
    /// <param name="Unit">Complete root-container membership.</param>
    /// <param name="Rows">Whether to compress rows rather than columns.</param>
    /// <param name="Compact">Private candidate reservations, never the live grid.</param>
    private static void CompressCellRoot(GridNode[] Unit, bool Rows, Dictionary<string, FlowchartCellRegion> Compact)
        {
            int? End = null; int Removed = 0;
            foreach (var Band in Unit.GroupBy(Node => Rows ? Compact[Node.Id].StartRow : Compact[Node.Id].StartColumn).OrderBy(Band => Band.Key).ToArray())
            {
                if (End.HasValue) Removed = checked(Removed + Math.Max(0, Band.Key - End.Value));
                int BandEnd = Band.Max(Node => Rows ? Compact[Node.Id].EndRow : Compact[Node.Id].EndColumn);
                foreach (var Node in Band)
                {
                    var Region = Compact[Node.Id];
                    Compact[Node.Id] = new(Rows ? checked(Region.StartRow - Removed) : Region.StartRow,
                        Rows ? Region.StartColumn : checked(Region.StartColumn - Removed), Region.RowSpan, Region.ColumnSpan);
                }
                End = End.HasValue ? Math.Max(End.Value, BandEnd) : BandEnd;
            }
        }
}
