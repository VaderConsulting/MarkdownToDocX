using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Offers whole-cell span repairs only after measuring allocated connection-boundary capacity.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Translates existing measured side-extent requirements into one complete whole-cell span assignment.</summary>
    /// <param name="Placed">All shared ownership, including obstacles and fixed hints.</param>
    /// <param name="Minimums">Measured required visible width and height by affected node.</param>
    /// <returns>Whether every required extent fit a complete legal reservation plan.</returns>
    internal static bool GrowCellMinimumExtents(List<GridNode> Placed, IReadOnlyDictionary<string, (double Width, double Height)> Minimums)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        return TryPlanCellMinimumExtents(Placed, Minimums, Before.Regions, out var Targets)
            && FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Builds measured span requirements on a caller's private ownership plan without applying any resize.</summary>
    /// <param name="Placed">Affected cell-backed nodes.</param>
    /// <param name="Minimums">Measured required visible extents.</param>
    /// <param name="Initial">Complete authoritative or private reservations.</param>
    /// <param name="Targets">Receives the complete private span plan.</param>
    /// <returns>Whether all measured requirements have a bounded whole-cell candidate.</returns>
    internal static bool TryPlanCellMinimumExtents(List<GridNode> Placed, IReadOnlyDictionary<string, (double Width, double Height)> Minimums,
        IReadOnlyDictionary<string, FlowchartCellRegion> Initial, out Dictionary<string, FlowchartCellRegion> Targets)
    {
        Targets = new Dictionary<string, FlowchartCellRegion>(Initial, StringComparer.Ordinal);
        foreach (var Node in Placed)
        {
            if (!Minimums.TryGetValue(Node.Id, out var Minimum)) continue;
            if (!double.IsFinite(Minimum.Width) || !double.IsFinite(Minimum.Height) || Minimum.Width <= 0 || Minimum.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(Minimums), "Measured minimum extents must be finite and positive.");
            var Region = Targets[Node.Id]; bool Fits = false;
            for (int Attempt = 0; Attempt < FlowchartLayoutRules.RepairCandidateMoves; Attempt++)
            {
                var Bounds = Node.CellNode!.Bounds(Region, Node.GeometryProvider);
                if (Bounds.Width >= Minimum.Width && Bounds.Height >= Minimum.Height) { Fits = true; break; }
                Region = new(Region.StartRow, Region.StartColumn, checked(Region.RowSpan + (Bounds.Height < Minimum.Height ? 1 : 0)),
                    checked(Region.ColumnSpan + (Bounds.Width < Minimum.Width ? 1 : 0)));
            }
            if (!Fits) return false;
            Targets[Node.Id] = Region;
        }
        return true;
    }

    /// <summary>Plans the complete crowded-side span assignment using the existing renderer-derived minimum port pitch.</summary>
    /// <param name="Placed">Complete diagram ownership, including fixed obstacles.</param>
    /// <param name="Counts">Allocated side demand from declared route endpoints and honoured side hints.</param>
    /// <param name="BoxWidth">Existing measured standard width used by the legacy pitch policy.</param>
    /// <param name="BoxHeight">Existing measured standard height used by the legacy pitch policy.</param>
    /// <returns>Whether the complete legal capacity plan was published.</returns>
    internal static bool GrowCellCrowdedSides(List<GridNode> Placed, IReadOnlyDictionary<string, int> Counts, int BoxWidth, int BoxHeight)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        foreach (var Pair in Counts)
        {
            string[] Parts = Pair.Key.Split('|');
            if (Parts.Length != 2 || !ById.TryGetValue(Parts[0], out var Node) || Pair.Value < 2 || Node.PlacementHintLocked) continue;
            bool Width = Parts[1] is "top" or "bottom";
            double Pitch = FlowchartLayoutRules.LineSpacing;
            if (TryPlanCellPortCapacity(Node, Targets[Node.Id], Parts[1], Pair.Value, Pitch, out var Candidate)) Targets[Node.Id] = Candidate;
        }
        // Sharing a newly reserved cell rejects every proposed resize. Later
        // joint allocation may choose a free side instead of growing the node.
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Builds a fitting whole-cell capacity proposal without changing any reservation or drawing coordinate.</summary>
    /// <param name="Node">Renderer-owned connection boundary.</param>
    /// <param name="Initial">Current or privately proposed reservation.</param>
    /// <param name="Side">Eligible compass side being allocated.</param>
    /// <param name="Count">Actual jointly allocated side demand.</param>
    /// <param name="Pitch">Existing measured minimum port separation.</param>
    /// <param name="Candidate">Receives the fitting complete reservation.</param>
    /// <returns>Whether a legal measured boundary capacity candidate was found within the existing named limit.</returns>
    internal static bool TryPlanCellPortCapacity(GridNode Node, FlowchartCellRegion Initial, string Side, int Count, double Pitch,
        out FlowchartCellRegion Candidate)
    {
        Candidate = Initial;
        bool Width = Side is "top" or "bottom";
        for (int Attempt = 0; Attempt < FlowchartLayoutRules.RepairCandidateMoves; Attempt++)
        {
            var Bounds = Node.CellNode!.Bounds(Candidate, Node.GeometryProvider);
            var Boundary = (Node.GeometryProvider?.Create(Bounds.Width, Bounds.Height) ?? NodeGeometry.Rectangle(Bounds.Width, Bounds.Height)).ConnectionBoundary;
            var Ports = new List<NodeGeometryPort>();
            for (int Index = 1; Index <= Count; Index++)
            {
                if (!Boundary.TryPort(Side, Index / (Count + 1d), out var Port)) break;
                Ports.Add(Port);
            }
            if (Ports.Count != Count) return false;
            if (FlowchartPortCapacity.Shortfall(Ports, Pitch) <= 0) return true;
            Candidate = new(Candidate.StartRow, Candidate.StartColumn, checked(Candidate.RowSpan + (Width ? 0 : 1)),
                checked(Candidate.ColumnSpan + (Width ? 1 : 0)));
        }
        return false;
    }
}
