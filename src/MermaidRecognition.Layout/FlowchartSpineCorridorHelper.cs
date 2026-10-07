using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Axis-aligned corridor around spine-aligned nodes for obstacle detection.
/// </summary>
/// <remarks>
/// Creates corridor bounds in layout coordinates.
/// </remarks>
public sealed class FlowchartSpineCorridorBounds(double Left, double Top, double Right, double Bottom)
{

    /// <summary>
    /// Minimum cross-axis coordinate of the corridor.
    /// </summary>
    public double Left { get; } = Left;

    /// <summary>
    /// Maximum flow-axis coordinate of the corridor (TB: top / larger Y).
    /// </summary>
    public double Top { get; } = Top;

    /// <summary>
    /// Maximum cross-axis coordinate of the corridor.
    /// </summary>
    public double Right { get; } = Right;

    /// <summary>
    /// Minimum flow-axis coordinate of the corridor (TB: bottom / smaller Y).
    /// </summary>
    public double Bottom { get; } = Bottom;
}

/// <summary>
/// Builds a padded corridor rectangle around the primary spine column.
/// </summary>
public static class FlowchartSpineCorridorHelper
{
    private const double CORRIDOR_PADDING_PX = 12.0;

    /// <summary>
    /// Builds corridor bounds covering all spine nodes with padding for intersection tests.
    /// </summary>
    public static bool TryBuildCorridor(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSpineLayoutContext Context,
        out FlowchartSpineCorridorBounds Corridor)
    {
        Corridor = null!;
        bool HasBounds = false;
        double Left = 0.0;
        double Right = 0.0;
        double Top = double.NegativeInfinity;
        double Bottom = double.PositiveInfinity;

        foreach (string SpineNodeId in Context.SpineNodeIds)
        {
            if (!BoundsById.TryGetValue(SpineNodeId, out FlowchartLayoutNodeBounds NodeBounds))
            {
                continue;
            }

            double NodeLeft = NodeBounds.CenterX - (NodeBounds.Width / 2.0);
            double NodeRight = NodeBounds.CenterX + (NodeBounds.Width / 2.0);
            double NodeTop = NodeBounds.CenterY + (NodeBounds.Height / 2.0);
            double NodeBottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0);

            if (!HasBounds)
            {
                Left = NodeLeft;
                Right = NodeRight;
                Top = NodeTop;
                Bottom = NodeBottom;
                HasBounds = true;
                continue;
            }

            Left = Math.Min(Left, NodeLeft);
            Right = Math.Max(Right, NodeRight);
            Top = Math.Max(Top, NodeTop);
            Bottom = Math.Min(Bottom, NodeBottom);
        }

        if (!HasBounds)
        {
            return false;
        }

        Corridor = new FlowchartSpineCorridorBounds(
            Left - CORRIDOR_PADDING_PX,
            Top + CORRIDOR_PADDING_PX,
            Right + CORRIDOR_PADDING_PX,
            Bottom - CORRIDOR_PADDING_PX);
        return true;
    }

    /// <summary>
    /// Builds the spine corridor from every spine node except <paramref name="ExcludedNodeId"/>.
    /// A decision branch starts inside its own diamond, so that box must not count as an obstacle.
    /// </summary>
    public static bool TryBuildCorridorExcluding(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSpineLayoutContext Context,
        string ExcludedNodeId,
        out FlowchartSpineCorridorBounds Corridor)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> Reduced =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (string SpineNodeId in Context.SpineNodeIds)
        {
            if (string.Equals(SpineNodeId, ExcludedNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (BoundsById.TryGetValue(SpineNodeId, out FlowchartLayoutNodeBounds NodeBounds))
            {
                Reduced[SpineNodeId] = NodeBounds;
            }
        }

        FlowchartSpineLayoutContext ReducedContext = new FlowchartSpineLayoutContext(
            new HashSet<string>(Reduced.Keys, StringComparer.Ordinal),
            Context.PrimaryRootId,
            Context.PrimarySinkId,
            Context.SpineCrossAxis,
            Context.ColumnStep);
        return TryBuildCorridor(Reduced, ReducedContext, out Corridor);
    }
}
