using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Spread top-face and bottom-face ports for left-to-right and right-to-left hubs with many parallel branches (F113).
/// The branch nodes sit in one column beside the hub. A branch whose node centre lies above the hub's top face leaves (or enters)
/// the hub through its own slot on the top face, and a branch below the bottom face uses a slot on the bottom face.
/// The farthest branch takes the slot farthest from the branch column, so the quarter curves nest without crossing
/// and no two arrowheads share a point. This is the horizontal-flow form of the F31/F32 hub side-face rule.
/// All coordinates are layout space (Y up).
/// </summary>
public static class FlowchartParallelBranchHubPortHelper
{
    private const int MINIMUM_BRANCH_COUNT = 4;
    private const double COLUMN_TOLERANCE_PX = 0.75;
    private const double SLOT_INSET_PX = 8.0;

    /// <summary>
    /// Resolves the route for an edge that leaves or enters a parallel-branch hub through a top-face or bottom-face slot.
    /// The hub is tried as the edge source first (fan-out), then as the edge target (fan-in).
    /// </summary>
    /// <param name="Edge">The edge to route.</param>
    /// <param name="Contract">The diagram contract.</param>
    /// <param name="BoundsById">Node bounds keyed by node id.</param>
    /// <param name="NodeById">Node contracts keyed by node id, used for shapes.</param>
    /// <param name="Direction">Diagram direction. Only left-to-right and right-to-left qualify.</param>
    /// <param name="LayoutPoints">Receives boundary, stub, stub, boundary from edge source to edge target.</param>
    /// <param name="HubIsSource">True when the hub is the edge source.</param>
    /// <returns>True when the edge uses a hub face slot.</returns>
    public static bool TryResolveEdgeRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints,
        out bool HubIsSource)
    {
        LayoutPoints = [];
        HubIsSource = false;

        if (Direction != FlowchartDirection.LeftToRight && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryResolveForHub(Edge, Contract, BoundsById, NodeById, Direction, true, LayoutPoints))
        {
            HubIsSource = true;
            return true;
        }

        LayoutPoints.Clear();
        return TryResolveForHub(Edge, Contract, BoundsById, NodeById, Direction, false, LayoutPoints);
    }

    private static bool TryResolveForHub(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        bool HubIsSource,
        List<(double X, double Y)> LayoutPoints)
    {
        string HubId = HubIsSource ? Edge.SourceId : Edge.TargetId;
        string OtherId = HubIsSource ? Edge.TargetId : Edge.SourceId;
        if (string.Equals(HubId, OtherId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!IsRectangleShape(HubId, NodeById))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(HubId, out FlowchartLayoutNodeBounds HubBounds) || !BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds OtherBounds))
        {
            return false;
        }

        List<FlowchartLayoutNodeBounds> Branches = CollectBranchBounds(Contract, BoundsById, HubId, HubIsSource);
        if (Branches.Count < MINIMUM_BRANCH_COUNT)
        {
            return false;
        }

        double HubHalfWidth = HubBounds.Width / 2.0;
        double HubHalfHeight = HubBounds.Height / 2.0;
        double ColumnX = OtherBounds.CenterX;
        bool ColumnIsRight = ColumnX > HubBounds.CenterX;
        foreach (FlowchartLayoutNodeBounds Branch in Branches)
        {
            if (Math.Abs(Branch.CenterX - ColumnX) > COLUMN_TOLERANCE_PX)
            {
                return false;
            }

            double BranchNearX = ColumnIsRight ? Branch.CenterX - (Branch.Width / 2.0) : Branch.CenterX + (Branch.Width / 2.0);
            double HubFarX = ColumnIsRight ? HubBounds.CenterX + HubHalfWidth : HubBounds.CenterX - HubHalfWidth;
            if (ColumnIsRight ? BranchNearX <= HubFarX : BranchNearX >= HubFarX)
            {
                return false;
            }
        }

        bool FlowGoesRight = Direction == FlowchartDirection.LeftToRight;
        bool ExpectedColumnIsRight = HubIsSource ? FlowGoesRight : !FlowGoesRight;
        if (ColumnIsRight != ExpectedColumnIsRight)
        {
            return false;
        }

        double HubTop = HubBounds.CenterY + HubHalfHeight;
        double HubBottom = HubBounds.CenterY - HubHalfHeight;
        bool UsesTopFace;
        if (OtherBounds.CenterY > HubTop)
        {
            UsesTopFace = true;
        }
        else if (OtherBounds.CenterY < HubBottom)
        {
            UsesTopFace = false;
        }
        else
        {
            return false;
        }

        List<FlowchartLayoutNodeBounds> FaceBranches = [];
        foreach (FlowchartLayoutNodeBounds Branch in Branches)
        {
            bool BranchOnFace = UsesTopFace ? Branch.CenterY > HubTop : Branch.CenterY < HubBottom;
            if (BranchOnFace)
            {
                FaceBranches.Add(Branch);
            }
        }

        FaceBranches.Sort((Left, Right) => Math.Abs(Right.CenterY - HubBounds.CenterY).CompareTo(Math.Abs(Left.CenterY - HubBounds.CenterY)));
        int SlotIndex = FaceBranches.FindIndex(Candidate => string.Equals(Candidate.NodeId, OtherId, StringComparison.Ordinal));
        if (SlotIndex < 0)
        {
            return false;
        }

        double HubMarkerGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(HubIsSource ? Edge.MarkerStart : Edge.MarkerEnd);
        double OtherMarkerGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(HubIsSource ? Edge.MarkerEnd : Edge.MarkerStart);
        double SlotX = ResolveSlotX(HubBounds, SlotIndex, FaceBranches.Count, ColumnIsRight);
        double FaceOutwardY = UsesTopFace ? 1.0 : -1.0;
        double HubY = UsesTopFace ? HubTop + HubMarkerGap : HubBottom - HubMarkerGap;
        double OtherOutwardX = ColumnIsRight ? -1.0 : 1.0;
        double OtherX = ColumnIsRight ? OtherBounds.CenterX - (OtherBounds.Width / 2.0) - OtherMarkerGap : OtherBounds.CenterX + (OtherBounds.Width / 2.0) + OtherMarkerGap;
        double OtherY = OtherBounds.CenterY;

        double HubStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(HubBounds);
        double OtherStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(OtherBounds);
        (double X, double Y) HubBoundary = (SlotX, HubY);
        (double X, double Y) HubStub = (SlotX, HubY + (FaceOutwardY * HubStubLength));
        (double X, double Y) OtherBoundary = (OtherX, OtherY);
        (double X, double Y) OtherStub = (OtherX + (OtherOutwardX * OtherStubLength), OtherY);

        if (HubIsSource)
        {
            LayoutPoints.Add(HubBoundary);
            LayoutPoints.Add(HubStub);
            LayoutPoints.Add(OtherStub);
            LayoutPoints.Add(OtherBoundary);
            return true;
        }

        LayoutPoints.Add(OtherBoundary);
        LayoutPoints.Add(OtherStub);
        LayoutPoints.Add(HubStub);
        LayoutPoints.Add(HubBoundary);
        return true;
    }

    /// <summary>
    /// Returns the slot X on the hub face. Slot 0 is the end of the face farthest from the branch column.
    /// A single slot sits at the face centre.
    /// </summary>
    private static double ResolveSlotX(FlowchartLayoutNodeBounds HubBounds, int SlotIndex, int SlotCount, bool ColumnIsRight)
    {
        if (SlotCount <= 1)
        {
            return HubBounds.CenterX;
        }

        double HalfWidth = HubBounds.Width / 2.0;
        double Inset = Math.Min(SLOT_INSET_PX, HalfWidth / 2.0);
        double Usable = HubBounds.Width - (2.0 * Inset);
        double Offset = Inset + (Usable * SlotIndex / (SlotCount - 1));
        if (ColumnIsRight)
        {
            return HubBounds.CenterX - HalfWidth + Offset;
        }

        return HubBounds.CenterX + HalfWidth - Offset;
    }

    private static List<FlowchartLayoutNodeBounds> CollectBranchBounds(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string HubId,
        bool HubIsSource)
    {
        List<FlowchartLayoutNodeBounds> Branches = [];
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || FlowchartEdgeGuidePointBuilder.UsesBottomFaceCorridor(Candidate, Contract))
            {
                continue;
            }

            string CandidateHubId = HubIsSource ? Candidate.SourceId : Candidate.TargetId;
            string CandidateOtherId = HubIsSource ? Candidate.TargetId : Candidate.SourceId;
            if (!string.Equals(CandidateHubId, HubId, StringComparison.Ordinal) || string.Equals(CandidateOtherId, HubId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!Seen.Add(CandidateOtherId))
            {
                continue;
            }

            if (BoundsById.TryGetValue(CandidateOtherId, out FlowchartLayoutNodeBounds OtherBounds))
            {
                Branches.Add(OtherBounds);
            }
        }

        return Branches;
    }

    private static bool IsRectangleShape(string NodeId, IReadOnlyDictionary<string, NodeContract> NodeById)
    {
        if (!NodeById.TryGetValue(NodeId, out NodeContract? Node))
        {
            return false;
        }

        string? Shape = Node.Shape;
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return true;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "rect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rounded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "square", StringComparison.OrdinalIgnoreCase);
    }
}
