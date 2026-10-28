using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Rejects proven semantic and fixed-frame faults before completing a band-layout candidate.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Checks final cell ownership and projected frame bounds without estimating routes or layout cost.</summary>
    /// <param name="State">Applied private candidate whose caller owns exact rollback.</param>
    /// <returns>Whether fixed semantic membership or a drawn frame already makes this candidate illegal.</returns>
    private static bool BandCandidateIsIllegal(GridLayoutState State)
    {
        if (!State.EnterpriseBands)
        {
            return false;
        }

        FlowchartLogicalGrid Grid = State.Placed[0].CellNode!.Grid;
        if (!Grid.CheckRegionConvention(Grid.Capture().Regions))
        {
            return true;
        }

        return !CellBandFramesAreLegal(State.Contract, State.Placed, Grid.Capture());
    }

    /// <summary>Checks pure projected frame geometry before a complete band-placement plan is published.</summary>
    /// <param name="Contract">Declared frame ancestry and clearance relationships.</param>
    /// <param name="Nodes">Cell-owned shape definitions in the existing stage direction.</param>
    /// <param name="Candidate">Complete unpublished cell and gap assignment.</param>
    /// <returns>Whether positive solid frames contain no unrelated frame or node conflicts.</returns>
    internal static bool CellBandFramesAreLegal(MermaidRecognition.Contract.DiagramContract Contract,
        System.Collections.Generic.List<GridNode> Nodes, FlowchartLogicalGrid.Snapshot Candidate)
    {
        List<GridFrame> Frames = ProjectCellFrames(Contract, Nodes, Candidate);
        return !Frames.Any(Frame => !double.IsFinite(Frame.Right - (double)Frame.Left)
            || Frame.Right <= Frame.Left || Frame.Top == Frame.Bottom)
            && CellFrameConflicts(Contract, Nodes, Frames, DiagramGridMetrics.GRID_UNIT_PX).Count == 0;
    }

    /// <summary>Accounts for proved rejected assignments without spending more than the caller's existing remaining slots.</summary>
    /// <param name="State">Owning diagram and persistent diagnostic counts.</param>
    /// <param name="Pass">Existing search whose budget is being consumed.</param>
    /// <param name="Count">Number of proved rejected assignments generated before the next usable option.</param>
    /// <param name="Remaining">Unspent slots in the named search limit.</param>
    /// <returns>Number of consumed slots; no routing is performed.</returns>
    private static int SpendBandPreflightSlots(GridLayoutState State, string Pass, int Count, int Remaining)
    {
        int Spent = Math.Min(Count, Math.Max(0, Remaining));
        if (Spent > 0)
        {
            State.Copy.RenderContext.BandPreflightDrops[Pass] = State.Copy.RenderContext.BandPreflightDrops.GetValueOrDefault(Pass) + Spent;
        }

        return Spent;
    }
}
