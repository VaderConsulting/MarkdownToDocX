using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Routing;
using Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    /// <summary>
    /// Check intersections between edges and obstacles using triangulation (faster than kd-tree)
    /// </summary>
    internal class CdtIntersections {
        readonly MetroGraphData metroGraphData;
        readonly BundlingSettings bundlingSettings;

        internal bool ComputeForcesForBundles = false;

        public CdtIntersections(MetroGraphData metroGraphData, BundlingSettings bundlingSettings) {
            this.metroGraphData = metroGraphData;
            this.bundlingSettings = bundlingSettings;
        }

        /// <summary>
        /// returns false iff the edge overlap an obstacle
        /// otherwise it calulates distances to the closest obstacles
        /// </summary>
        internal bool BundleAvoidsObstacles(Station v, Station u, Point vPosition, Point uPosition, double upperBound,
            out List<Tuple<Point, Point>> closestDist) {

            closestDist = [];
            //return true;

            Set<Polyline> obstaclesToIgnore = metroGraphData.looseIntersections.ObstaclesToIgnoreForBundle(v, u);
            Dictionary<Polyline, Tuple<Point, Point>> closeObstacles = FindCloseObstaclesForBundle(u.CdtTriangle, uPosition, vPosition, obstaclesToIgnore, upperBound);
            if (closeObstacles == null) return false;

            //Polyline bundle = Intersections.Create4gon(vPosition, uPosition, upperBound, upperBound);

            foreach (KeyValuePair<Polyline, Tuple<Point, Point>> item in closeObstacles) {
                Tuple<Point, Point> dist = item.Value;

                //TODO: get rif od this call!
                //if (!Intersections.ClosedPolylinesIntersect(bundle, obstacle)) continue;
                //if (!Curve.ClosedCurveInteriorsIntersect(bundle, obstacle)) continue;

                closestDist.Add(dist);
           } 

            return true;
        }

        internal bool HubAvoidsObstacles(Station v, Point vPosition, double upperBound,
            out List<double> closestDist) {

            closestDist = [];
            Set<Polyline> obstaclesToIgnore = metroGraphData.looseIntersections.ObstaclesToIgnoreForHub(v);
            Dictionary<Polyline, double> closeObstacles = FindCloseObstaclesForHub(v.CdtTriangle, v.Position, vPosition, obstaclesToIgnore, upperBound);
            if (closeObstacles == null) return false;

            foreach (KeyValuePair<Polyline, double> item in closeObstacles) {
                double dist = item.Value;

                closestDist.Add(dist);
            }

            return true;
        }

        /// <summary>
        /// returns null iff the edge overlap an obstacle
        /// </summary>
        Dictionary<Polyline, Tuple<Point, Point>> FindCloseObstaclesForBundle(CdtTriangle startTriangle, Point start,
                                                                              Point end, Set<Polyline> obstaclesToIgnore,
                                                                              double upperBound) {
            Dictionary<Polyline, Tuple<Point, Point>> obstacles = new Dictionary<Polyline, Tuple<Point, Point>>();
            if (!ThreadLineSegmentThroughTriangles(startTriangle, start, end, obstaclesToIgnore, out List<CdtTriangle> list))
                return null;

            if (!ComputeForcesForBundles && !bundlingSettings.HighestQuality)
                return obstacles;

            HashSet<CdtSite> checkedSites = new HashSet<CdtSite>();

            foreach (CdtTriangle t in list) {
                foreach (CdtSite s in t.Sites) {
                    if (!checkedSites.Add(s)) continue;

                    Polyline poly = (Polyline) s.Owner;
                    if (obstaclesToIgnore.Contains(poly)) continue;

                    PolylinePoint pp = FindPolylinePoint(poly, s.Point);
                    double d12 = LineSegment.MinDistBetweenLineSegments(pp.Point, pp.NextOnPolyline.Point, start, end,
                                                                  out double par11, out double par12);
                    double d22 = LineSegment.MinDistBetweenLineSegments(pp.Point, pp.PrevOnPolyline.Point, start, end,
                                                                  out double par21, out double par22);
                    Point r1, r2;
                    double dist;
                    if (d12 < d22) {
                        dist = d12;
                        if (dist > upperBound) continue;
                        r1 = pp.Point + (pp.NextOnPolyline.Point - pp.Point)*par11;
                        r2 = start + (end - start)*par12;
                    }
                    else {
                        dist = d22;
                        if (dist > upperBound) continue;
                        r1 = pp.Point + (pp.PrevOnPolyline.Point - pp.Point)*par21;
                        r2 = start + (end - start)*par22;
                    }
                    //if (dist > upperBound) continue;

                    if (!obstacles.TryGetValue(poly, out Tuple<Point, Point> currentValue))
                        obstacles.Add(poly, new Tuple<Point, Point>(r1, r2));
                }
            }

            return obstacles;
        }

        Dictionary<Polyline, double> FindCloseObstaclesForHub(CdtTriangle startTriangle, Point start, Point end, Set<Polyline> obstaclesToIgnore, double upperBound) {
            Dictionary<Polyline, double> obstacles = new Dictionary<Polyline, double>();
            if (!ThreadLineSegmentThroughTriangles(startTriangle, start, end, obstaclesToIgnore, out List<CdtTriangle> nearestTriangles))
                return null;

            HashSet<CdtSite> checkedSites = new HashSet<CdtSite>();

            foreach (CdtTriangle t in nearestTriangles) {
                foreach (CdtSite s in t.Sites) {
                    CheckSite(end, obstaclesToIgnore, checkedSites, s, upperBound, obstacles);

                    CdtEdge edge = t.OppositeEdge(s);
                    CdtTriangle ot = edge.GetOtherTriangle(t);
                    if (ot != null) {
                        CheckSite(end, obstaclesToIgnore, checkedSites, ot.OppositeSite(edge), upperBound, obstacles);
                    }
                }
            }

            return obstacles;
        }

        void CheckSite(Point end, Set<Polyline> obstaclesToIgnore, HashSet<CdtSite> checkedSites, CdtSite s, double upperBound, Dictionary<Polyline, double> obstacles) {
            if (!checkedSites.Add(s)) return;

            Polyline poly = (Polyline)s.Owner;
            if (obstaclesToIgnore.Contains(poly)) return;

            //distance to the obstacle
            PolylinePoint pp = FindPolylinePoint(poly, s.Point);
            double d12 = Point.DistToLineSegment(end, pp.Point, pp.NextOnPolyline.Point, out double par);
            double d22 = Point.DistToLineSegment(end, pp.Point, pp.PrevOnPolyline.Point, out par);
            double dist = Math.Min(d12, d22);
            if (dist > upperBound) return;

            if (!obstacles.TryGetValue(poly, out double currentValue))
            {
                obstacles.Add(poly, dist);
            }
            else if (currentValue > dist)
            {
                obstacles[poly] = dist;
            }
        }
        
        /// <summary>
        /// returns false iff the edge overlap an obstacle
        /// </summary>
        bool ThreadLineSegmentThroughTriangles(CdtTriangle currentTriangle, Point start, Point end, Set<Polyline> obstaclesToIgnore,
            out List<CdtTriangle> triangles) {
            Debug.Assert(Cdt.PointIsInsideOfTriangle(start, currentTriangle));
            triangles = [];

            if (Cdt.PointIsInsideOfTriangle(end, currentTriangle)) {
                triangles.Add(currentTriangle);
                return true;
            }

            CdtThreader threader = new CdtThreader(currentTriangle, start, end);
            triangles.Add(currentTriangle);

            while (threader.MoveNext()) {
                triangles.Add(threader.CurrentTriangle);
                CdtEdge piercedEdge = threader.CurrentPiercedEdge;
                if (piercedEdge.Constrained) {
                    Debug.Assert(piercedEdge.lowerSite.Owner == piercedEdge.upperSite.Owner);
                    Polyline poly = (Polyline) piercedEdge.lowerSite.Owner;
                    if (!obstaclesToIgnore.Contains(poly)) return false;
                }                
            }
            if (threader.CurrentTriangle != null)
                triangles.Add(threader.CurrentTriangle);
//
//            int positiveSign, negativeSign;
//            CdtEdge piercedEdge = FindFirstPiercedEdge(currentTriangle, start, end, out negativeSign, out positiveSign,  null);
//            
//            Debug.Assert(positiveSign > negativeSign);
//
//            Debug.Assert(piercedEdge != null);
//
//            do {
//                triangles.Add(currentTriangle);
//                if (piercedEdge.Constrained) {
//                    Debug.Assert(piercedEdge.lowerSite.Owner == piercedEdge.upperSite.Owner);
//                    Polyline poly = (Polyline)piercedEdge.lowerSite.Owner;
//                    if (!obstaclesToIgnore.Contains(poly)) return false;
//                }
//            }
//            while (FindNextPierced(start, end, ref currentTriangle, ref piercedEdge, ref negativeSign, ref positiveSign));
//            if (currentTriangle != null)
//                triangles.Add(currentTriangle);

            return true;
        }

        static internal int GetHyperplaneSign(Point start, Point end, CdtSite cdtSite) {
            double area = Point.SignedDoubledTriangleArea(start, cdtSite.Point, end);
            if (area > ApproximateComparer.DistanceEpsilon) return 1;
            if (area < -ApproximateComparer.DistanceEpsilon) return -1;
            return 0;
        }

        internal static PointLocation PointLocationInsideTriangle(Point p, CdtTriangle triangle) {
            bool seenBoundary=false;
            for (int i = 0; i < 3; i++) {
                double area = Point.SignedDoubledTriangleArea(p, triangle.Sites[i].Point, triangle.Sites[i + 1].Point);
                if (area < -ApproximateComparer.DistanceEpsilon)
                    return PointLocation.Outside;
                if (area < ApproximateComparer.DistanceEpsilon)
                    seenBoundary = true;
            }

            return seenBoundary ? PointLocation.Boundary : PointLocation.Inside;
        }

        
        static PolylinePoint FindPolylinePoint(Polyline poly, Point point) {
            foreach (PolylinePoint ppp in poly.PolylinePoints)
                if (ppp.Point == point)
                    return ppp;

            throw new NotSupportedException();
        }

        /// <summary>
        /// checks if an edge intersects obstacles
        /// otherwise it calulates distances to the closest obstacles
        /// </summary>
        internal bool EdgeIsLegal(Station v, Station u, Point vPosition, Point uPosition) {
            Set<Polyline> obstaclesToIgnore = metroGraphData.looseIntersections.ObstaclesToIgnoreForBundle(v, u);
            return ThreadLineSegmentThroughTriangles(v.CdtTriangle, vPosition, uPosition, obstaclesToIgnore, out List<CdtTriangle> list);
        }

        /// <summary>
        /// checks if an edge intersects obstacles
        /// otherwise it calulates distances to the closest obstacles
        /// </summary>
        internal bool EdgeIsLegal(Station v, Station u, Point vPosition, Point uPosition, Set<Polyline> obstaclesToIgnore) {
            Point start = v.Position;

            CdtTriangle currentTriangle = v.CdtTriangle;
            Debug.Assert(Cdt.PointIsInsideOfTriangle(start, currentTriangle));

            Point end = u.Position;
            if (Cdt.PointIsInsideOfTriangle(end, currentTriangle)) {
                return true;
            }

            CdtThreader threader = new CdtThreader(currentTriangle, start, end);
            
            while (threader.MoveNext()) {
                CdtEdge piercedEdge = threader.CurrentPiercedEdge;
                if (piercedEdge.Constrained) {
                    Debug.Assert(piercedEdge.lowerSite.Owner == piercedEdge.upperSite.Owner);
                    Polyline poly = (Polyline)piercedEdge.lowerSite.Owner;
                    if (!obstaclesToIgnore.Contains(poly)) return false;
                }
            }
            return true;
        }

    }
}
