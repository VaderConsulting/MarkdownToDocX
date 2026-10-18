using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

internal static partial class FlowchartLayoutScore
{
    /// <summary>Caches unchanged line costs while allowing authoritative node and frame bounds to vary between cell candidates.</summary>
    internal sealed class CellMovementContext
    {
        private readonly DiagramContract Contract;
        private readonly FlowchartLayoutResult Layout;
        private readonly FlowchartLayoutEdgeRoute[] Routes;
        private readonly (double X, double Y)[][] Points;
        private readonly int[] Bends;
        private readonly int[] Disconnections;
        private readonly int[,] Crossings;
        private readonly int[,] SharedCrossings;
        private readonly List<FlowchartLayoutQualityFailure>[] Faults;

        /// <summary>Captures reference pair costs and immutable routes once for the restored search base.</summary>
        /// <param name="Contract">Unchanged topology and hints.</param>
        /// <param name="Layout">Live candidate model with a stable route inventory.</param>
        internal CellMovementContext(DiagramContract Contract, FlowchartLayoutResult Layout)
        {
            this.Contract = Contract; this.Layout = Layout;
            Routes = Layout.EdgeRoutes.ToArray(); Points = Routes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
            Bends = Routes.Select(Route => CountCorners(Route.Points)).ToArray();
            var Indexes = DisconnectionIndexes(Contract, Layout);
            Disconnections = Routes.Select(Route => CountRouteDisconnections(Route, Indexes.Bounds, Indexes.Shapes, Indexes.Edges)).ToArray();
            Crossings = new int[Routes.Length, Routes.Length]; SharedCrossings = new int[Routes.Length, Routes.Length];
            Faults = Routes.Select(Route => { var Values = new List<FlowchartLayoutQualityFailure>(); DiagramGridQuality.CollectRouteFailures(Layout, Route, Values); return Values; }).ToArray();
            for (int A = 0; A < Routes.Length; A++)
            for (int B = A + 1; B < Routes.Length; B++)
            {
                Crossings[A, B] = PairCrossings(Routes[A], Points[A], Routes[B], Points[B]);
                SharedCrossings[A, B] = PairSharedEndpointCrossings(Routes[A], Points[A], Routes[B], Points[B]);
            }
        }

        /// <summary>Recomputes moved geometry costs and only affected route costs and interactions, retaining exact unaffected bend totals.</summary>
        /// <param name="Affected">Routes whose ports, obstacles or paths changed.</param>
        /// <returns>The same complete weighted measurement as the reference scorer for this fixed candidate.</returns>
        internal Result Measure(IReadOnlySet<FlowchartLayoutEdgeRoute> Affected)
        {
            var Geometry = new FlowchartLayoutResult { UsesGridRoutes = Layout.UsesGridRoutes };
            Geometry.NodeBoundsMutable.AddRange(Layout.NodeBounds); Geometry.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
            var Violations = new List<FlowchartLayoutQualityFailure>(); DiagramGridQuality.Collect(Contract, Geometry, Violations, false);
            var Indexes = DisconnectionIndexes(Contract, Layout);
            int Corners = 0, Disconnected = 0, Cross = 0, Shared = 0;
            var Current = Routes.Select((Route, Index) => Affected.Contains(Route) ? Route.Points.Select(Point => (Point.X, Point.Y)).ToArray() : Points[Index]).ToArray();
            for (int A = 0; A < Routes.Length; A++)
            {
                if (Affected.Contains(Routes[A]))
                {
                    Corners += CountCorners(Routes[A].Points);
                    Disconnected += CountRouteDisconnections(Routes[A], Indexes.Bounds, Indexes.Shapes, Indexes.Edges);
                    DiagramGridQuality.CollectRouteFailures(Layout, Routes[A], Violations);
                }
                else { Corners += Bends[A]; Disconnected += Disconnections[A]; Violations.AddRange(Faults[A]); }
                for (int B = A + 1; B < Routes.Length; B++)
                {
                    bool Changed = Affected.Contains(Routes[A]) || Affected.Contains(Routes[B]);
                    Cross += Changed ? PairCrossings(Routes[A], Current[A], Routes[B], Current[B]) : Crossings[A, B];
                    Shared += Changed ? PairSharedEndpointCrossings(Routes[A], Current[A], Routes[B], Current[B]) : SharedCrossings[A, B];
                }
            }
            return MeasureCore(Contract, Layout, false, Cross, Corners, Violations,
                DisconnectionsOverride: Disconnected, SharedEndpointCrossings: Shared);
        }
    }
}
