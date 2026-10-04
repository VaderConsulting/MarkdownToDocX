using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.GraphAlgorithms;
using Microsoft.AGL.DebugHelpers;
using Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation;

namespace Microsoft.AGL.Core.Layout.ProximityOverlapRemoval.MST
{
    /// <summary>
    /// Computes the minimum spanning tree on a triangulation or on a set of edges given by a list of tuple.
    /// </summary>
    public class MstOnDelaunayTriangulation
    {


        /// <summary>
        /// Computes the minimum spanning tree on a set of edges
        /// </summary>
        /// <param name="proximityEdges">list of tuples, each representing an edge with: nodeId1, nodeId2, t(overlapFactor), ideal distance, edge weight.</param>
        /// <param name="sizeId"></param>
        /// <returns></returns>
        internal static List<Tuple<int, int, double, double, double>> GetMstOnTuple(List<Tuple<int,int,double,double,double>> proximityEdges, int sizeId) {
            if (proximityEdges.Count == 0)
            {
                return null;
            }
            IntPair[] intPairs = proximityEdges.Select(t => new IntPair(t.Item1, t.Item2)).ToArray();
            Dictionary<IntPair, Tuple<int, int, double, double, double>> weighting = new Dictionary<IntPair, Tuple<int, int, double, double, double>>(intPairs.Count());
            for (int i = 0; i < proximityEdges.Count; i++) {
                weighting[intPairs[i]] = proximityEdges[i];
            }
            BasicGraph<IEdge> graph = new BasicGraph<IEdge>(intPairs, sizeId);

            MinimumSpanningTreeByPrim mstOnBasicGraph = new MinimumSpanningTreeByPrim(graph, intPair => weighting[(IntPair)intPair].Item5, intPairs[0].First);

            List<Tuple<int, int, double, double, double>> treeEdges = mstOnBasicGraph.GetTreeEdges().Select(e => weighting[(IntPair) e]).ToList();
            return treeEdges;
        }

        /// <summary>
        /// Computes the minimum spanning tree on a DT with given weights.
        /// </summary>
        /// <param name="cdt"></param>
        /// <param name="weights"></param>
        /// <returns></returns>
        internal static List<CdtEdge> GetMstOnCdt(Cdt cdt, Func<CdtEdge, double> weights) {
            CdtSite[] siteArray = cdt.PointsToSites.Values.ToArray();
            Dictionary<CdtSite, int> siteIndex = new Dictionary<CdtSite, int>();
            for (int i = 0; i < siteArray.Length; i++)
            {
                siteIndex[siteArray[i]] = i;
            }

            Dictionary<IntPair, CdtEdge> intPairsToCdtEdges = GetEdges(siteArray, siteIndex);

            BasicGraph<IEdge> graph = new BasicGraph<IEdge>( intPairsToCdtEdges.Keys, siteArray.Length);

            MinimumSpanningTreeByPrim mstOnBasicGraph = new MinimumSpanningTreeByPrim(graph, intPair => weights(intPairsToCdtEdges[(IntPair)intPair]), 0);

            return new List<CdtEdge>(mstOnBasicGraph.GetTreeEdges().Select(e=>intPairsToCdtEdges[(IntPair)e]));
        }

        static Dictionary<IntPair, CdtEdge> GetEdges(CdtSite[] siteArray, Dictionary<CdtSite, int> siteIndex) {
            Dictionary<IntPair, CdtEdge> d = new Dictionary<IntPair, CdtEdge>();
            for (int i = 0; i < siteArray.Length; i++) {
                CdtSite site = siteArray[i];
                int sourceIndex = siteIndex[site];
                foreach (CdtEdge e in site.Edges)
                {
                    d[new IntPair(sourceIndex, siteIndex[e.lowerSite])] = e;
                }
            }
            return d;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void Test()
        {
#if DEBUG && !SILVERLIGHT && !SHARPKIT
            int count = 100;
            Random random = new Random(3);
            List<Point> points = new List<Point>();
            for (int i = 0; i < count; i++)
            {
                points.Add(20 * new Point(random.NextDouble(), random.NextDouble()));
            }

            Cdt cdt = new Cdt(points, null, null);
            cdt.Run();
            List<CdtEdge> ret = GetMstOnCdt(cdt, e => (e.lowerSite.Point - e.upperSite.Point).Length);
            List<DebugCurve> l = new List<DebugCurve>();
            foreach(CdtSite s in cdt.PointsToSites.Values)
            {
                foreach (CdtEdge e in s.Edges)
                {
                    l.Add(new DebugCurve(100, 0.1, "black", new LineSegment(e.lowerSite.Point, e.upperSite.Point)));
                }
            }

            foreach (CdtEdge e in ret)
            {
                l.Add(new DebugCurve(100, 0.12, "red", new LineSegment(e.lowerSite.Point, e.upperSite.Point)));
            }
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(l);
#endif
        }
    }
}
