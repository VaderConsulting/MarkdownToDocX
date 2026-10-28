using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

internal static partial class FlowchartStarFanNesting
{
    /// <summary>Publishes a complete route option only when its finished layout adds no finding and worsens no bend/crossing count.</summary>
    /// <param name="Contract">Unchanged source.</param>
    /// <param name="Layout">Live model; only route records may change.</param>
    /// <param name="Proposed">Complete replacement paths, slots and normals.</param>
    /// <returns>Whether the option survived; rejection restores exact old route objects and values.</returns>
    internal static bool TryPublish(DiagramContract Contract, FlowchartLayoutResult Layout,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> Proposed)
    {
        (int Bends, int Crossings, double Shared) Before = Measure(Layout.EdgeRoutes);
        Dictionary<(string Rule, string Subject), int> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout).GroupBy(Finding => (Finding.Rule, Finding.Subject))
            .ToDictionary(Group => Group.Key, Group => Group.Count());
        FlowchartLayoutScore.ComparisonCost OldScore = FlowchartLayoutScore.Measure(Contract, Layout, false).Cost;
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        Dictionary<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> Saved = Proposed.Keys.ToDictionary(Route => Route, Clone);
        bool Keep = false;
        try
        {
            foreach (KeyValuePair<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> Pair in Proposed)
            {
                Copy(Pair.Value, Pair.Key);
            }

            (int Bends, int Crossings, double Shared) After = Measure(Layout.EdgeRoutes);
            if (After.Bends > Before.Bends || After.Crossings > Before.Crossings || After.Shared > Before.Shared)
            {
                return false;
            }

            if (Proposed.Keys.Any(Route => !FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route,
                Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())))
            {
                return false;
            }

            if (FlowchartRouteRuleChecker.Check(Contract, Layout).GroupBy(Finding => (Finding.Rule, Finding.Subject))
                .Any(Group => Group.Count() > Findings.GetValueOrDefault(Group.Key)))
            {
                return false;
            }

            if (FlowchartLayoutScore.Measure(Contract, Layout, false).Cost >= OldScore)
            {
                return false;
            }

            Keep = true; return true;
        }
        finally { if (!Keep)
            {
                foreach (KeyValuePair<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> Pair in Saved)
                {
                    Copy(Pair.Value, Pair.Key);
                }
            }
        }
    }

    /// <summary>Builds an independent route record so a discarded search owns no live points or cached guides.</summary>
    /// <param name="Route">Stored model route.</param>
    /// <returns>Deep terminal/path copy with the original declared identity.</returns>
    internal static FlowchartLayoutEdgeRoute Clone(FlowchartLayoutEdgeRoute Route)
    {
        FlowchartLayoutEdgeRoute Result = new FlowchartLayoutEdgeRoute { EdgeId = Route.EdgeId, SourceId = Route.SourceId, TargetId = Route.TargetId };
        Copy(Route, Result); return Result;
    }

}
