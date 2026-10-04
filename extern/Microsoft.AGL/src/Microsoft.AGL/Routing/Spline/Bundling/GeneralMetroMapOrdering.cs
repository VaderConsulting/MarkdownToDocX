using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.DebugHelpers;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    /// <summary>
    /// greedy bundle map ordering based on path comparison
    /// </summary>
    public class GeneralMetroMapOrdering : IMetroMapOrderingAlgorithm {
        /// <summary>
        /// bundle lines
        /// </summary>
        readonly List<Metroline> Metrolines;
        Dictionary<PointPair, PointPairOrder> bundles;

        internal Metroline GetMetroline(int metroEdgeIndex) => Metrolines[metroEdgeIndex];
        /// <summary>
        /// Initialize bundle graph and build the ordering
        /// </summary>
        internal GeneralMetroMapOrdering(List<Metroline> Metrolines) {
            this.Metrolines = Metrolines;

            BuildOrder();
        }

        /// <summary>
        /// Get the ordering of lines on station u with respect to the edge (u->v)
        /// </summary>
#if SHARPKIT //http://code.google.com/p/sharpkit/issues/detail?id=203
        //SharpKit/Colin - Interface implementations
        public IEnumerable<Metroline> GetOrder(Station u, Station v) {
#else
        IEnumerable<Metroline> IMetroMapOrderingAlgorithm.GetOrder(Station u, Station v) {
#endif
            PointPair pointPair = new PointPair(u.Position, v.Position);
            List<Metroline> orderedMetrolineListForUv = bundles[pointPair].Metrolines;
            if (u.Position == pointPair.First) {
                foreach (Metroline Metroline in orderedMetrolineListForUv) {
                    yield return Metroline;
                }
            }
            else {
                for (int i = orderedMetrolineListForUv.Count - 1; i >= 0; i--)
                {
                    yield return orderedMetrolineListForUv[i];
                }
            }
        }


        /// <summary>
        /// Get the number of lines passing through edge (u->v)
        /// </summary>
        internal int GetLineCount(Point u, Point v) => bundles[new PointPair(u, v)].Count;

        /// <summary>
        /// Get the index of line on the edge (u->v) and node u
        /// </summary>
#if SHARPKIT //http://code.google.com/p/sharpkit/issues/detail?id=203
        //SharpKit/Colin - Interface implementations
        public int GetLineIndexInOrder(Station u, Station v, Metroline Metroline) {
#else
        int IMetroMapOrderingAlgorithm.GetLineIndexInOrder(Station u, Station v, Metroline Metroline) {
#endif
            PointPair edge = new PointPair(u.Position, v.Position);
            bool reversed = u.Position != edge.First;
            Dictionary<Metroline, int> d = bundles[edge].LineIndexInOrder;
            return !reversed ? d[Metroline] : d.Count - 1 - d[Metroline];
        }


        
        /// <summary>
        /// Do the main job
        /// </summary>
        void BuildOrder() {
            bundles = [];
            
            //initialization
            foreach (Metroline Metroline in Metrolines) {
                for (PolylinePoint p = Metroline.Polyline.StartPoint; p.Next != null; p = p.Next) {
                    PointPair e = new PointPair(p.Point, p.Next.Point);
                    if (!bundles.TryGetValue(e, out PointPairOrder li))
                    {
                        bundles[e] = li = new PointPairOrder();
                    }

                    li.Add(Metroline);
                }
            }

            foreach (KeyValuePair<PointPair, PointPairOrder> edge in bundles)
            {
                BuildOrder(edge.Key, edge.Value);
            }
        }

        /// <summary>
        /// Build order for edge (u->v)
        /// </summary>
        void BuildOrder(PointPair pair, PointPairOrder order) {
            if (order.orderFixed)
            {
                return;
            }

            order.Metrolines.Sort((line0, line1) => CompareLines(line0, line1, pair.First, pair.Second));

            //save order
            order.orderFixed = true;
            order.LineIndexInOrder = [];
            for (int i = 0; i < order.Metrolines.Count; i++)
            {
                order.LineIndexInOrder[order.Metrolines[i]] = i;
            }
        }




        /// <summary>
        /// Compare two lines on station u with respect to edge (u->v)
        /// </summary>
        int CompareLines(Metroline ml0, Metroline ml1, Point u, Point v) {
            FindStationOnLine(u, v, ml0, out PolylinePoint polylinePoint0, out Func<PolylinePoint, PolylinePoint> next0, out Func<PolylinePoint, PolylinePoint> prev0);
            FindStationOnLine(u, v, ml1, out PolylinePoint polylinePoint1, out Func<PolylinePoint, PolylinePoint> next1, out Func<PolylinePoint, PolylinePoint> prev1);

            //go backward
            PolylinePoint p0 = polylinePoint0;
            PolylinePoint p1 = polylinePoint1;
            PolylinePoint p00, p11 = null;

            while ((p00 = prev0(p0)) != null && (p11 = prev1(p1)) != null && p00.Point == p11.Point) {
                PointPair edge = new PointPair(p00.Point, p0.Point);
                if (bundles[edge].orderFixed) {
                    return CompareOnFixedOrder(edge, ml0, ml1, p00.Point !=edge.First);
                }
                p0 = p00;
                p1 = p11;
            }

            if (p00 != null && p11 != null) {   //we have a backward fork
                Point forkBase = p0.Point;
                return IsLeft(next0(p0).Point - forkBase,
                              p00.Point - forkBase,
                              p11.Point - forkBase);
            }

            //go forward
            p0 = polylinePoint0;
            p1 = polylinePoint1;
            while ((p00 = next0(p0)) != null && (p11 = next1(p1)) != null && p00.Point == p11.Point) {
                PointPair edge = new PointPair(p00.Point, p0.Point);

                if (bundles[edge].orderFixed)
                {
                    return CompareOnFixedOrder(edge, ml0, ml1, p0.Point!=edge.First);
                }

                p0 = p00;
                p1 = p11;
            }


            if (p00 != null && p11 != null) {//compare forward fork
                Point forkBase = p0.Point;
                return -IsLeft(prev0(p0).Point - forkBase,
                              p00.Point - forkBase,
                              p11.Point - forkBase);
            }

            //these are multiple edges
            return ml0.Index.CompareTo(ml1.Index);
        }

        int CompareOnFixedOrder(PointPair edge, Metroline ml0, Metroline ml1, bool reverse) {
            Dictionary<Metroline, int> mlToIndex = bundles[edge].LineIndexInOrder;
            int r = reverse ? -1 : 1;
            return r * mlToIndex[ml0].CompareTo(mlToIndex[ml1]);
        }


        /// <summary>
        /// Reimplement it in more efficient way!!! (cache indexes)
        /// </summary>
        void FindStationOnLine(Point u, Point v, Metroline Metroline, out PolylinePoint polyPoint, out Func<PolylinePoint, PolylinePoint> next,
            out Func<PolylinePoint, PolylinePoint> prev) {

            for (PolylinePoint p = Metroline.Polyline.StartPoint; p.Next != null; p = p.Next) {
                if (p.Point == u && p.Next.Point == v) {
                    next = Next;
                    prev = Prev;
                    polyPoint = p;
                    return;
                }

                if (p.Point == v && p.Next.Point == u) {
                    prev = Next;
                    next = Prev;
                    polyPoint = p.Next;
                    return;
                }
            }
            throw new InvalidOperationException();
        }

        static PolylinePoint Next(PolylinePoint p) => p.Next;
        static PolylinePoint Prev(PolylinePoint p) => p.Prev;

        /// <summary>
        /// Compare polar angles of v1 and v2 with respect to v0
        /// (v1 lyes to the left of v2 ?)
        /// </summary>
        /// <returns>
        ///  -1  if v1 lyes to the left of v2
        ///   1  if v1 lyes to the right of v2
        ///   0  if v1 and v2 are collinear (and codirectinal)
        /// </returns>
        static int IsLeft(Point v0, Point v1, Point v2) => Point.GetOrientationOf3Vectors(v0, v1, v2);
    }

    
}
