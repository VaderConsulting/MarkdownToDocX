using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Core.Routing;
using Microsoft.AGL.Routing.Visibility;
using Microsoft.AGL.DebugHelpers;
using Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation;
using Microsoft.AGL.Core.Geometry.RTree;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    /// <summary>
    /// Stores intersections between edges, hubs, and obstacles to speed up simulated annealing
    /// </summary>
    internal class IntersectionCache {
        readonly MetroGraphData metroGraphData;
        readonly BundlingSettings bundlingSettings;
        readonly CostCalculator costCalculator;
        readonly Cdt cdt;

        public IntersectionCache(MetroGraphData metroGraphData, BundlingSettings bundlingSettings, CostCalculator costCalculator, Cdt cdt) {
            Debug.Assert(cdt!=null);
            this.metroGraphData = metroGraphData;
            this.bundlingSettings = bundlingSettings;
            this.costCalculator = costCalculator;
            this.cdt = cdt;
        }

        internal void InitializeCostCache() {
            foreach (Station v in metroGraphData.VirtualNodes()) {
                v.cachedIdealRadius = HubRadiiCalculator.CalculateIdealHubRadiusWithNeighbors(metroGraphData, bundlingSettings, v);
                v.cachedRadiusCost = costCalculator.RadiusCost(v, v.Position);
                v.cachedBundleCost = 0;
            }

            foreach (Tuple<Station, Station> edge in metroGraphData.VirtualEdges()) {
                Station v = edge.Item1;
                Station u = edge.Item2;
                StationEdgeInfo edgeInfo = metroGraphData.GetIjInfo(v, u);
                edgeInfo.cachedBundleCost = costCalculator.BundleCost(v, u, v.Position);
                v.cachedBundleCost += edgeInfo.cachedBundleCost;
                u.cachedBundleCost += edgeInfo.cachedBundleCost;
            }
        }

        internal void UpdateCostCache(Station node) {
            RectangleNode<CdtTriangle> cdtTree = cdt.GetCdtTree();
            node.CdtTriangle = cdtTree.FirstHitNode(node.Position, Test).UserData;

            node.cachedIdealRadius = HubRadiiCalculator.CalculateIdealHubRadiusWithNeighbors(metroGraphData, bundlingSettings, node);
            node.cachedRadiusCost = costCalculator.RadiusCost(node, node.Position);
            node.cachedBundleCost = 0;

            foreach (Station adj in node.Neighbors) {
                if (!adj.IsRealNode) {
                    adj.cachedIdealRadius = HubRadiiCalculator.CalculateIdealHubRadiusWithNeighbors(metroGraphData, bundlingSettings, adj);
                    adj.cachedRadiusCost = costCalculator.RadiusCost(adj, adj.Position);
                }

                StationEdgeInfo edgeInfo = metroGraphData.GetIjInfo(node, adj);
                adj.cachedBundleCost -= edgeInfo.cachedBundleCost;

                edgeInfo.cachedBundleCost = costCalculator.BundleCost(node, adj, node.Position);
                node.cachedBundleCost += edgeInfo.cachedBundleCost;
                adj.cachedBundleCost += edgeInfo.cachedBundleCost;
            }
        }

        internal static HitTestBehavior Test(Point pnt, CdtTriangle t) => Cdt.PointIsInsideOfTriangle(pnt, t) ? HitTestBehavior.Stop : HitTestBehavior.Continue;

    }
}