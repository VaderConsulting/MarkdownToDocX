using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Places the two targets of a two-way decision on either side of the diamond, so each branch can leave
/// from its own shoulder (see <c>Mermaid_Specification.md</c> §16.4.2).
/// The diamond, and the plain chain of nodes that feeds it, is centred between the two targets.
/// When both targets sit in the same column, the target that only the decision feeds moves sideways first.
/// </summary>
public static class FlowchartDecisionShoulderLayoutPostProcessor
{
    private const double ALIGNED_TOLERANCE_PX = 1.0;

    private const double MINIMUM_SHOULDER_CLEARANCE_PX = 16.0;

    /// <summary>
    /// Adjusts top-to-bottom and bottom-to-top layouts so every two-way decision straddles its targets.
    /// Diagrams with subgraphs and left-to-right diagrams are returned unchanged.
    /// </summary>
    /// <param name="Contract">The parsed flowchart.</param>
    /// <param name="Source">Node bounds in layout coordinates (Y up).</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <param name="Options">Render options, used for node spacing.</param>
    /// <returns>The adjusted node bounds.</returns>
    public static List<FlowchartLayoutNodeBounds> Apply(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        List<FlowchartLayoutNodeBounds> Result = new List<FlowchartLayoutNodeBounds>(Source);
        if (Direction != FlowchartDirection.TopToBottom && Direction != FlowchartDirection.BottomToTop)
        {
            return Result;
        }

        if (Contract.Groups.Count > 0 || Source.Count < 3)
        {
            return Result;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        double FlowSign = Direction == FlowchartDirection.TopToBottom ? -1.0 : 1.0;
        List<NodeContract> Decisions = Contract.Nodes.Where(Node => IsDecisionShape(Node.Shape) && BoundsById.ContainsKey(Node.Id)).ToList();
        Decisions.Sort((Left, Right) => (BoundsById[Right.Id].CenterY * FlowSign).CompareTo(BoundsById[Left.Id].CenterY * FlowSign));

        foreach (NodeContract Decision in Decisions)
        {
            TryStraddleTargets(Contract, BoundsById, Decision.Id, FlowSign, Options);
        }

        return BoundsById.Values.ToList();
    }

    private static void TryStraddleTargets(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string DecisionId,
        double FlowSign,
        FlowchartRenderOptions Options)
    {
        List<string> TargetIds = GetVisibleSuccessorIds(Contract, DecisionId);
        if (TargetIds.Count != 2)
        {
            return;
        }

        FlowchartLayoutNodeBounds DecisionBounds = BoundsById[DecisionId];
        if (!BoundsById.TryGetValue(TargetIds[0], out FlowchartLayoutNodeBounds FirstTarget) || !BoundsById.TryGetValue(TargetIds[1], out FlowchartLayoutNodeBounds SecondTarget))
        {
            return;
        }

        if (!IsDownstream(DecisionBounds, FirstTarget, FlowSign) || !IsDownstream(DecisionBounds, SecondTarget, FlowSign))
        {
            return;
        }

        double ShoulderOffset = DecisionBounds.Width / 4.0;
        if (Straddles(DecisionBounds.CenterX, ShoulderOffset, FirstTarget.CenterX, SecondTarget.CenterX))
        {
            return;
        }

        if (Math.Abs(FirstTarget.CenterX - SecondTarget.CenterX) < ALIGNED_TOLERANCE_PX)
        {
            if (!TryMoveSingleInputTargetAside(Contract, BoundsById, DecisionId, FirstTarget, SecondTarget, FlowSign, Options))
            {
                return;
            }

            FirstTarget = BoundsById[TargetIds[0]];
            SecondTarget = BoundsById[TargetIds[1]];
        }

        double MidpointX = (FirstTarget.CenterX + SecondTarget.CenterX) / 2.0;
        if (!Straddles(MidpointX, ShoulderOffset, FirstTarget.CenterX, SecondTarget.CenterX))
        {
            return;
        }

        List<string> ChainIds = BuildFeedingChain(Contract, BoundsById, DecisionId, FlowSign);
        double DeltaX = MidpointX - DecisionBounds.CenterX;
        TryShiftNodes(BoundsById, ChainIds, DeltaX, Options.NodeSpacingPx / 2.0);
    }

    private static bool TryMoveSingleInputTargetAside(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string DecisionId,
        FlowchartLayoutNodeBounds FirstTarget,
        FlowchartLayoutNodeBounds SecondTarget,
        double FlowSign,
        FlowchartRenderOptions Options)
    {
        FlowchartLayoutNodeBounds DecisionBounds = BoundsById[DecisionId];
        FlowchartLayoutNodeBounds NearTarget = FirstTarget;
        FlowchartLayoutNodeBounds FarTarget = SecondTarget;
        if (Math.Abs(SecondTarget.CenterY - DecisionBounds.CenterY) < Math.Abs(FirstTarget.CenterY - DecisionBounds.CenterY))
        {
            NearTarget = SecondTarget;
            FarTarget = FirstTarget;
        }

        FlowchartLayoutNodeBounds Mover = NearTarget;
        FlowchartLayoutNodeBounds Anchor = FarTarget;
        if (GetVisiblePredecessorIds(Contract, Mover.NodeId).Count != 1)
        {
            Mover = FarTarget;
            Anchor = NearTarget;
            if (GetVisiblePredecessorIds(Contract, Mover.NodeId).Count != 1)
            {
                return false;
            }
        }

        double Distance = Math.Max((Mover.Width + Anchor.Width) / 2.0 + Options.NodeSpacingPx, (DecisionBounds.Width / 2.0) + (MINIMUM_SHOULDER_CLEARANCE_PX * 2.0));
        double PreferredSign = ResolveFreeSide(Contract, BoundsById, Anchor, DecisionId, Mover.NodeId);
        double Margin = Options.NodeSpacingPx / 2.0;
        if (TryShiftNodes(BoundsById, [Mover.NodeId], PreferredSign * Distance, Margin))
        {
            return true;
        }

        return TryShiftNodes(BoundsById, [Mover.NodeId], -PreferredSign * Distance, Margin);
    }

    /// <summary>
    /// Returns +1 (right) or -1 (left): the side of <paramref name="Anchor"/> that its other inputs do not come from.
    /// </summary>
    private static double ResolveFreeSide(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds Anchor,
        string DecisionId,
        string MoverId)
    {
        double OffsetSum = 0.0;
        foreach (string PredecessorId in GetVisiblePredecessorIds(Contract, Anchor.NodeId))
        {
            if (string.Equals(PredecessorId, DecisionId, StringComparison.Ordinal) || string.Equals(PredecessorId, MoverId, StringComparison.Ordinal))
            {
                continue;
            }

            if (BoundsById.TryGetValue(PredecessorId, out FlowchartLayoutNodeBounds PredecessorBounds))
            {
                OffsetSum += PredecessorBounds.CenterX - Anchor.CenterX;
            }
        }

        if (OffsetSum > ALIGNED_TOLERANCE_PX)
        {
            return -1.0;
        }

        return 1.0;
    }

    /// <summary>
    /// Returns the decision plus each upstream node that feeds only into the next node of the chain,
    /// stopping at a node with more than one input or a node that also feeds elsewhere.
    /// </summary>
    private static List<string> BuildFeedingChain(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string DecisionId,
        double FlowSign)
    {
        List<string> ChainIds = [DecisionId];
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal) { DecisionId };
        string CurrentId = DecisionId;
        while (true)
        {
            List<string> PredecessorIds = GetVisiblePredecessorIds(Contract, CurrentId);
            if (PredecessorIds.Count != 1)
            {
                break;
            }

            string PredecessorId = PredecessorIds[0];
            if (Visited.Contains(PredecessorId) || !BoundsById.TryGetValue(PredecessorId, out FlowchartLayoutNodeBounds PredecessorBounds))
            {
                break;
            }

            if (GetVisibleSuccessorIds(Contract, PredecessorId).Count != 1)
            {
                break;
            }

            if (!IsDownstream(PredecessorBounds, BoundsById[CurrentId], FlowSign))
            {
                break;
            }

            ChainIds.Add(PredecessorId);
            Visited.Add(PredecessorId);
            CurrentId = PredecessorId;
        }

        return ChainIds;
    }

    private static bool TryShiftNodes(
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<string> NodeIds,
        double DeltaX,
        double Margin)
    {
        if (Math.Abs(DeltaX) < ALIGNED_TOLERANCE_PX)
        {
            return true;
        }

        HashSet<string> MovingIds = new HashSet<string>(NodeIds, StringComparer.Ordinal);
        foreach (string NodeId in NodeIds)
        {
            FlowchartLayoutNodeBounds Moving = BoundsById[NodeId];
            double MovedCentreX = Moving.CenterX + DeltaX;
            foreach (FlowchartLayoutNodeBounds Other in BoundsById.Values)
            {
                if (MovingIds.Contains(Other.NodeId))
                {
                    continue;
                }

                bool OverlapsX = Math.Abs(MovedCentreX - Other.CenterX) < ((Moving.Width + Other.Width) / 2.0) + Margin;
                bool OverlapsY = Math.Abs(Moving.CenterY - Other.CenterY) < ((Moving.Height + Other.Height) / 2.0) + Margin;
                if (OverlapsX && OverlapsY)
                {
                    return false;
                }
            }
        }

        foreach (string NodeId in NodeIds)
        {
            FlowchartLayoutNodeBounds Moving = BoundsById[NodeId];
            BoundsById[NodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Moving.NodeId,
                CenterX = Moving.CenterX + DeltaX,
                CenterY = Moving.CenterY,
                Width = Moving.Width,
                Height = Moving.Height
            };
        }

        return true;
    }

    private static bool Straddles(double DecisionCentreX, double ShoulderOffset, double FirstTargetX, double SecondTargetX)
    {
        double LeftX = Math.Min(FirstTargetX, SecondTargetX);
        double RightX = Math.Max(FirstTargetX, SecondTargetX);
        return LeftX <= DecisionCentreX - ShoulderOffset - MINIMUM_SHOULDER_CLEARANCE_PX && RightX >= DecisionCentreX + ShoulderOffset + MINIMUM_SHOULDER_CLEARANCE_PX;
    }

    private static bool IsDownstream(FlowchartLayoutNodeBounds Upstream, FlowchartLayoutNodeBounds Downstream, double FlowSign)
    {
        return (Downstream.CenterY - Upstream.CenterY) * FlowSign > ALIGNED_TOLERANCE_PX;
    }

    private static List<string> GetVisibleSuccessorIds(DiagramContract Contract, string NodeId)
    {
        List<string> SuccessorIds = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal) && !string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal) && !SuccessorIds.Contains(Edge.TargetId))
            {
                SuccessorIds.Add(Edge.TargetId);
            }
        }

        return SuccessorIds;
    }

    private static List<string> GetVisiblePredecessorIds(DiagramContract Contract, string NodeId)
    {
        List<string> PredecessorIds = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal) && !string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal) && !PredecessorIds.Contains(Edge.SourceId))
            {
                PredecessorIds.Add(Edge.SourceId);
            }
        }

        return PredecessorIds;
    }

    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase) || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }
}
