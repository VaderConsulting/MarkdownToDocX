using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Offers whole-cell span repairs only after measuring allocated connection-boundary capacity.</summary>
public static partial class DiagramGridLayout
{
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
            double Pitch = Math.Max(1, (Width ? BoxWidth : BoxHeight) / DiagramGridMetrics.TRACKS_PER_GAP);
            var Region = Targets[Node.Id];
            var Candidate = Region;
            bool Fits = false;
            for (int Attempt = 0; Attempt < FlowchartLayoutRules.RepairCandidateMoves; Attempt++)
            {
                var Bounds = Node.CellNode!.Bounds(Candidate, Node.GeometryProvider);
                var Boundary = (Node.GeometryProvider?.Create(Bounds.Width, Bounds.Height)
                    ?? NodeGeometry.Rectangle(Bounds.Width, Bounds.Height)).ConnectionBoundary;
                var Ports = new List<NodeGeometryPort>();
                for (int Index = 1; Index <= Pair.Value; Index++)
                {
                    if (!Boundary.TryPort(Parts[1], Index / (Pair.Value + 1d), out var Port)) break;
                    Ports.Add(Port);
                }
                // An ineligible side needs reassignment, not blind growth.
                if (Ports.Count != Pair.Value) break;
                if (FlowchartPortCapacity.Shortfall(Ports, Pitch) <= 0) { Fits = true; break; }
                Candidate = new(Candidate.StartRow, Candidate.StartColumn, checked(Candidate.RowSpan + (Width ? 0 : 1)),
                    checked(Candidate.ColumnSpan + (Width ? 1 : 0)));
            }
            if (Fits) Targets[Node.Id] = Candidate;
        }
        // Sharing a newly reserved cell rejects every proposed resize. Later
        // joint allocation may choose a free side instead of growing the node.
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }
}
