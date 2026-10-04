//
// ObstaclePortEntrance.cs
// MSAGL class for a single entrance point on an Obstacle for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    /// <summary>
    /// An ObstaclePortEntrance is a single edge entering or leaving an obstacle in one of the NSEW Compass directions.
    /// </summary>
    internal class ObstaclePortEntrance {
        ObstaclePort ObstaclePort { get; set; }
        Obstacle Obstacle => ObstaclePort.Obstacle;

        // The intersection point on the obstacle border (e.g. intersection with a port point, or
        // midpoint of PortEntry) and the direction from that point to find the outer vertex.
        internal Point UnpaddedBorderIntersect { get; private set; }
        internal Directions OutwardDirection { get; private set; }
        internal Point VisibilityBorderIntersect { get; private set; }
        internal bool IsOverlapped { get; private set; }
        internal double InitialWeight => IsOverlapped ? ScanSegment.OverlappedWeight : ScanSegment.NormalWeight;
        private double unpaddedToPaddedBorderWeight = ScanSegment.NormalWeight;

        internal bool IsCollinearWithPort => CompassVector.IsPureDirection(PointComparer.GetDirections(VisibilityBorderIntersect, ObstaclePort.Location));

        // The line segment from VisibilityBorderIntersect to the first obstacle it hits.
        internal LineSegment MaxVisibilitySegment { get; private set; }
        private readonly PointAndCrossingsList pointAndCrossingsList;
        internal bool IsVertical => StaticGraphUtility.IsVertical(MaxVisibilitySegment);

        // If the port has entrances that are collinear, don't do the optimization for non-collinear ones.
        internal bool WantVisibilityIntersection => !IsOverlapped && CanExtend && (!ObstaclePort.HasCollinearEntrances || IsCollinearWithPort);
        internal bool CanExtend => PointComparer.GetDirections(MaxVisibilitySegment.Start, MaxVisibilitySegment.End) != Directions.None;

        internal ObstaclePortEntrance(ObstaclePort oport, Point unpaddedBorderIntersect, Directions outDir, ObstacleTree obstacleTree) {
            ObstaclePort = oport;
            UnpaddedBorderIntersect = unpaddedBorderIntersect;
            OutwardDirection = outDir;

            // Get the padded intersection.
            LineSegment lineSeg = new LineSegment(UnpaddedBorderIntersect, StaticGraphUtility.RectangleBorderIntersect(
                                            oport.Obstacle.VisibilityBoundingBox, UnpaddedBorderIntersect, outDir));
            IList<IntersectionInfo> xxs = Curve.GetAllIntersections(lineSeg, oport.Obstacle.VisibilityPolyline, true /*liftIntersections*/);
            Debug.Assert(1 == xxs.Count, "Expected one intersection");
            VisibilityBorderIntersect = ApproximateComparer.Round(SpliceUtility.RawIntersection(xxs[0], UnpaddedBorderIntersect));

            MaxVisibilitySegment = obstacleTree.CreateMaxVisibilitySegment(VisibilityBorderIntersect,
                    OutwardDirection, out pointAndCrossingsList);

            // Groups are never in a clump (overlapped) but they may still have their port entrance overlapped.
            if (Obstacle.IsOverlapped || (Obstacle.IsGroup && !Obstacle.IsInConvexHull)) {
                IsOverlapped = obstacleTree.IntersectionIsInsideAnotherObstacle(/*sideObstacle:*/ null, Obstacle
                        , VisibilityBorderIntersect, ScanDirection.GetInstance(OutwardDirection));
                if (!Obstacle.IsGroup || IsOverlapped || InteriorEdgeCrossesObstacle(obstacleTree)) {
                    unpaddedToPaddedBorderWeight = ScanSegment.OverlappedWeight;
                }
            }
            if (Obstacle.IsInConvexHull && (unpaddedToPaddedBorderWeight == ScanSegment.NormalWeight)) {
                SetUnpaddedToPaddedBorderWeightFromHullSiblingOverlaps(obstacleTree);
            }
        }

        private void SetUnpaddedToPaddedBorderWeightFromHullSiblingOverlaps(ObstacleTree obstacleTree) {
            if (Obstacle.IsGroup ? InteriorEdgeCrossesObstacle(obstacleTree) : InteriorEdgeCrossesConvexHullSiblings()) {
                unpaddedToPaddedBorderWeight = ScanSegment.OverlappedWeight;
            }
        }

        private bool InteriorEdgeCrossesObstacle(ObstacleTree obstacleTree) {
            // File Test: Nudger_Overlap4
            // Use the VisibilityBoundingBox for groups because those are what the tree consists of.
            Rectangle rect = new Rectangle(UnpaddedBorderIntersect, VisibilityBorderIntersect);
            return InteriorEdgeCrossesObstacle(rect, obs => obs.VisibilityPolyline,
                    obstacleTree.Root.GetLeafRectangleNodesIntersectingRectangle(rect)
                        .Where(node => !node.UserData.IsGroup && (node.UserData != Obstacle)).Select(node => node.UserData!));
        }

        private bool InteriorEdgeCrossesConvexHullSiblings() {
            // There is no RectangleNode tree that includes convex hull non-primary siblings, so we just iterate;
            // this will only be significant to perf in extremely overlapped cases that we are not optimizing for.
            Rectangle rect = new Rectangle(UnpaddedBorderIntersect, VisibilityBorderIntersect);
            return InteriorEdgeCrossesObstacle(rect, obs => obs.PaddedPolyline,
                    Obstacle.ConvexHull.Obstacles.Where(obs => obs != Obstacle));
        }

        private bool InteriorEdgeCrossesObstacle(Rectangle rect, Func<Obstacle, Polyline> whichPolylineToUse, IEnumerable<Obstacle> candidates) {
            LineSegment lineSeg = null;
            foreach (Obstacle blocker in candidates) {
                Polyline blockerPolyline = whichPolylineToUse(blocker);
                if (!StaticGraphUtility.RectangleInteriorsIntersect(rect, blockerPolyline.BoundingBox)) {
                    continue;
                }
                lineSeg = lineSeg ?? new LineSegment(UnpaddedBorderIntersect, VisibilityBorderIntersect);
                IntersectionInfo xx = Curve.CurveCurveIntersectionOne(lineSeg, blockerPolyline, liftIntersection: false);
                if (xx != null) {
                    return true;
                }
                if (PointLocation.Outside != Curve.PointRelativeToCurveLocation(UnpaddedBorderIntersect, blockerPolyline)) {
                    return true;
                }
            }
            return false;
        }

        internal bool HasGroupCrossings => (pointAndCrossingsList != null) && (pointAndCrossingsList.Count > 0);

        internal bool HasGroupCrossingBeforePoint(Point point) {
            if (!HasGroupCrossings) {
                return false;
            }
            PointAndCrossings pac = StaticGraphUtility.IsAscending(OutwardDirection) ? pointAndCrossingsList.First : pointAndCrossingsList.Last;
            return PointComparer.GetDirections(MaxVisibilitySegment.Start, pac.Location) == PointComparer.GetDirections(pac.Location, point);
        }

        internal void AddToAdjacentVertex(TransientGraphUtility transUtil, VisibilityVertex targetVertex
                            , Rectangle limitRect, bool routeToCenter) {
            VisibilityVertex borderVertex = transUtil.VisGraph.FindVertex(VisibilityBorderIntersect);
            if (null != borderVertex) {
                ExtendFromBorderVertex(transUtil, borderVertex, limitRect, routeToCenter);
                return;
            }

            // There is no vertex at VisibilityBorderIntersect, so create it and link it to targetVertex.
            // Note: VisibilityBorderIntersect may == targetIntersect if that is on our border, *and*
            // targetIntersect may be on the border of a touching obstacle, in which case this will splice
            // into or across the adjacent obstacle, which is consistent with "touching is overlapped".
            // So we don't use UnpaddedBorderIntersect as prevPoint when calling ExtendEdgeChain.

            // VisibilityBorderIntersect may be rounded just one Curve.DistanceEpsilon beyond the ScanSegment's
            // perpendicular coordinate; e.g. our X may be targetIntersect.X + Curve.DistanceEpsilon, thereby
            // causing the direction from VisibilityBorderIntersect to targetIntersect to be W instead of E.
            // So use the targetIntersect if they are close enough; they will be equal for flat borders, and
            // otherwise the exact value we use only needs be "close enough" to the border.  (We can't use
            // CenterVertex as the prevPoint because that could be an impure direction).
            // Update: With the change to carry MaxVisibilitySegment within the PortEntrance, PortManager finds
            // targetVertex between VisibilityBorderIntersect and MaxVisibilitySegment.End, so this should no longer
            // be able to happen.
            // See RectilinearTests.PaddedBorderIntersectMeetsIncomingScanSegment for an example of what happens
            // when VisibilityBorderIntersect is on the incoming ScanSegment (it jumps out above with borderVertex found).
            if (OutwardDirection == PointComparer.GetPureDirection(targetVertex.Point, VisibilityBorderIntersect)) {
                Debug.Assert(false, "Unexpected reversed direction between VisibilityBorderIntersect and targetVertex");
// ReSharper disable HeuristicUnreachableCode
                VisibilityBorderIntersect = targetVertex.Point;
                borderVertex = targetVertex;
// ReSharper restore HeuristicUnreachableCode
            }
            else {
                borderVertex = transUtil.FindOrAddVertex(VisibilityBorderIntersect);
                transUtil.FindOrAddEdge(borderVertex, targetVertex,  InitialWeight);
            }
            ExtendEdgeChain(transUtil, borderVertex, targetVertex, limitRect, routeToCenter);
        }

        internal void ExtendFromBorderVertex(TransientGraphUtility transUtil, VisibilityVertex borderVertex
                            , Rectangle limitRect, bool routeToCenter) => ExtendEdgeChain(transUtil, borderVertex, borderVertex, limitRect, routeToCenter);

        internal void ExtendEdgeChain(TransientGraphUtility transUtil, VisibilityVertex paddedBorderVertex
                                , VisibilityVertex targetVertex, Rectangle limitRect, bool routeToCenter) {
            // Extend the edge chain to the opposite side of the limit rectangle.
            transUtil.ExtendEdgeChain(targetVertex, limitRect, MaxVisibilitySegment, pointAndCrossingsList, IsOverlapped);

            // In order for Nudger to be able to map from the (near-) endpoint vertex to a PortEntry, we must 
            // always connect a vertex at UnpaddedBorderIntersect to the paddedBorderVertex, even if routeToCenter.
            VisibilityVertex unpaddedBorderVertex = transUtil.FindOrAddVertex(UnpaddedBorderIntersect);
            transUtil.FindOrAddEdge(unpaddedBorderVertex, paddedBorderVertex, unpaddedToPaddedBorderWeight);
            if (routeToCenter) {
                // Link the CenterVertex to the vertex at UnpaddedBorderIntersect.
                transUtil.ConnectVertexToTargetVertex(ObstaclePort.CenterVertex, unpaddedBorderVertex, OutwardDirection, InitialWeight);
            }
        }

        internal void RemoveFromGraph() {
            // Currently all transient removals and edge restorations are done by TransientGraphUtility itself
            // and ObstaclePort clears itself so there's nothing to do.
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1}~{2} {3}",
                                ObstaclePort.Location, UnpaddedBorderIntersect, VisibilityBorderIntersect, OutwardDirection);
    }
}