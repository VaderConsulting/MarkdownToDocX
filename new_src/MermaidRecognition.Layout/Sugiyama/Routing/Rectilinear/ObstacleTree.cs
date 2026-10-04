//
// ObstacleTree.cs
// MSAGL class for wrapping a RectangleNode<Obstacle> hierarchy for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    using System;

    using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
    using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
    using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms.CycleRemoval;

    internal class ObstacleTree {
        /// <summary>
        /// The root of the hierarchy.
        /// </summary>
        internal RectangleNode<Obstacle> Root { get; private set; }
        internal Rectangle GraphBox => Root.Rectangle;

        /// <summary>
        /// Dictionary of sets of ancestors for each shape, for evaluating necessary group-boundary crossings.
        /// </summary>
        internal Dictionary<Shape, Set<Shape>> AncestorSets;

        /// <summary>
        /// Indicates whether we adjusted spatial ancestors due to blocked paths.
        /// </summary>
        internal bool SpatialAncestorsAdjusted;

        /// <summary>
        /// The map of shapes to obstacles.
        /// </summary>
        private Dictionary<Shape, Obstacle> shapeIdToObstacleMap;

        /// <summary>
        /// The map of all group boundary crossings for the current RestrictSegmentWithObstacles call.
        /// </summary>
        internal readonly GroupBoundaryCrossingMap CurrentGroupBoundaryCrossingMap = new GroupBoundaryCrossingMap();

        /// <summary>
        /// The list of all obstacles (not just those in the Root, which may have accretions of obstacles in convex hulls).
        /// </summary>
        private List<Obstacle> allObstacles;

        /// <summary>
        /// For accreting obstacles for clumps or convex hulls.
        /// </summary>
        private readonly Set<IntPair> overlapPairs = [];

        /// <summary>
        /// Indicates whether one or more obstacles overlap.
        /// </summary>
        private bool hasOverlaps;

        /// <summary>
        /// Member to avoid unnecessary class creation just to do a lookup.
        /// </summary>
        private readonly IntPair lookupIntPair = new IntPair(-1, -1);

        /// <summary>
        /// Create the tree hierarchy from the enumeration.
        /// </summary>
        /// <param name="obstacles"></param>
        /// <param name="ancestorSets"></param>
        /// <param name="idToObstacleMap"></param>
        internal void Init(IEnumerable<Obstacle> obstacles, Dictionary<Shape, Set<Shape>> ancestorSets
                        , Dictionary<Shape, Obstacle> idToObstacleMap) {
            CreateObstacleListAndOrdinals(obstacles);
            AncestorSets = ancestorSets;
            CreateRoot();
            shapeIdToObstacleMap = idToObstacleMap;
        }

        private void CreateObstacleListAndOrdinals(IEnumerable<Obstacle> obstacles) {
            allObstacles = obstacles.ToList();
            int scanlineOrdinal = Obstacle.FirstNonSentinelOrdinal;
            foreach (Obstacle obstacle in allObstacles) {
                obstacle.Ordinal = scanlineOrdinal++;
            }
        }

        private Obstacle OrdinalToObstacle(int index) {
            Debug.Assert(index >= Obstacle.FirstNonSentinelOrdinal, "index too small");
            Debug.Assert(index < allObstacles.Count + Obstacle.FirstNonSentinelOrdinal, "index too large");
            return allObstacles[index - Obstacle.FirstNonSentinelOrdinal];
        }

        /// <summary>
        /// The number of obstacles.
        /// </summary>
        internal int Count => shapeIdToObstacleMap.Count;

        /// <summary>
        /// Create the root with overlapping non-rectangular obstacles converted to their convex hulls, for more reliable calculations.
        /// </summary>
        private void CreateRoot() {
            Root = CalculateHierarchy(GetAllObstacles());
            if (!OverlapsExist()) {
                return;
            }
            AccreteClumps();
            AccreteConvexHulls();
            GrowGroupsToAccommodateOverlaps();
            Root = CalculateHierarchy(GetAllObstacles().Where(obs => obs.IsPrimaryObstacle));
        }

        private bool OverlapsExist() {
            if (Root == null) {
                return false;
            }
            RectangleNodeUtils.CrossRectangleNodes<Obstacle>(Root, Root, CheckForInitialOverlaps);
            return hasOverlaps;
        }

        private bool OverlapPairAlreadyFound(Obstacle a, Obstacle b) {
            // If we already found it then we'll have enqueued it in the reverse order.
            lookupIntPair.x = b.Ordinal;
            lookupIntPair.y = a.Ordinal;
            return overlapPairs.Contains(lookupIntPair);
        }

        private void CheckForInitialOverlaps(Obstacle a, Obstacle b) {
            if (hasOverlaps) {
                return;
            }

            if (ObstaclesIntersect(a, b, out bool aIsInsideB, out bool bIsInsideA))
            {
                hasOverlaps = true;
                return;
            }
            if (!aIsInsideB && !bIsInsideA) {
                return;
            }

            // One obstacle is inside the other.  If they're both groups, or a non-group is inside a group, nothing
            // further is needed; we process groups differently because we can go through their sides.
            if (a.IsGroup && b.IsGroup) {
                return;
            }
            if ((a.IsGroup && bIsInsideA) || (b.IsGroup && aIsInsideB)) {
                return;
            }
            hasOverlaps = true;
        }

        private void AccreteClumps() {
            // Clumps are only created once.  After that, as the result of convex hull creation, we may
            // overlap an obstacle of a clump, in which case we enclose the clump in the convex hull as well.
            // We only allow clumps of rectangular obstacles, to avoid angled sides in the scanline.
            AccumulateObstaclesForClumps();
            if (overlapPairs.Count == 0) {
                return;
            }
            CreateClumps();
        }

        private void AccreteConvexHulls() {
            // Convex-hull creation is transitive, because the created hull may overlap additional obstacles.
            for (; ; ) {
                AccumulateObstaclesForConvexHulls();
                if (!CreateConvexHulls()) {
                    return;
                }
            }
        }

        internal static RectangleNode<Obstacle> CalculateHierarchy(IEnumerable<Obstacle> obstacles) {
            List<RectangleNode<Obstacle>> rectNodes = obstacles.Select(obs => new RectangleNode<Obstacle>(obs, obs.VisibilityBoundingBox)).ToList();
            return RectangleNode<Obstacle>.CreateRectangleNodeOnListOfNodes(rectNodes);
        }

        private void AccumulateObstaclesForClumps() {
            overlapPairs.Clear();
            RectangleNode<Obstacle> rectangularObstacles = CalculateHierarchy(GetAllObstacles().Where(obs => !obs.IsGroup && obs.IsRectangle));
            if (rectangularObstacles == null) {
                return;
            }
            RectangleNodeUtils.CrossRectangleNodes<Obstacle>(rectangularObstacles, rectangularObstacles, EvaluateOverlappedPairForClump);
        }

        private void EvaluateOverlappedPairForClump(Obstacle a, Obstacle b) {
            Debug.Assert(!a.IsGroup && !b.IsGroup, "Groups should not come here");
            Debug.Assert(a.IsRectangle && b.IsRectangle, "Only rectangles should come here");
            if ((a == b) || OverlapPairAlreadyFound(a, b)) {
                return;
            }

            if (!ObstaclesIntersect(a, b, out bool aIsInsideB, out bool bIsInsideA) && !aIsInsideB && !bIsInsideA)
            {
                return;
            }
            overlapPairs.Insert(new IntPair(a.Ordinal, b.Ordinal));
        }

        private void AccumulateObstaclesForConvexHulls() {
            overlapPairs.Clear();
            RectangleNode<Obstacle> allPrimaryNonGroupObstacles = CalculateHierarchy(GetAllObstacles().Where(obs => obs.IsPrimaryObstacle && !obs.IsGroup));
            if (allPrimaryNonGroupObstacles == null) {
                return;
            }
            RectangleNodeUtils.CrossRectangleNodes<Obstacle>(allPrimaryNonGroupObstacles, allPrimaryNonGroupObstacles, EvaluateOverlappedPairForConvexHull);
        }

        private void EvaluateOverlappedPairForConvexHull(Obstacle a, Obstacle b) {
            Debug.Assert(!a.IsGroup && !b.IsGroup, "Groups should not come here");
            if ((a == b) || OverlapPairAlreadyFound(a, b)) {
                return;
            }

            if (!ObstaclesIntersect(a, b, out bool aIsInsideB, out bool bIsInsideA) && !aIsInsideB && !bIsInsideA)
            {
                return;
            }

            // If either is in a convex hull, those must be coalesced.
            if (!a.IsInConvexHull && !b.IsInConvexHull) {
                // If the obstacles are rectangles, we don't need to do anything (for this pair).
                if (a.IsRectangle && b.IsRectangle) {
                    return;
                }
            }

            overlapPairs.Insert(new IntPair(a.Ordinal, b.Ordinal));
            AddClumpToConvexHull(a);
            AddClumpToConvexHull(b);
            AddConvexHullToConvexHull(a);
            AddConvexHullToConvexHull(b);
        }

        void GrowGroupsToAccommodateOverlaps() {
            // Group growth is transitive, because the created hull may overlap additional obstacles.
            for (; ; ) {
                AccumulateObstaclesForGroupOverlaps();
                if (!GrowGroupsToResolveOverlaps()) {
                    return;
                }
            }
        }

        private void AccumulateObstaclesForGroupOverlaps() {
            RectangleNode<Obstacle> groupObstacles = CalculateHierarchy(GetAllObstacles().Where(obs => obs.IsGroup));
            RectangleNode<Obstacle> allPrimaryObstacles = CalculateHierarchy(GetAllObstacles().Where(obs => obs.IsPrimaryObstacle));
            if ((groupObstacles == null) || (allPrimaryObstacles == null)) {
                return;
            }
            RectangleNodeUtils.CrossRectangleNodes<Obstacle>(groupObstacles, allPrimaryObstacles, EvaluateOverlappedPairForGroup);
        }

        private void EvaluateOverlappedPairForGroup(Obstacle a, Obstacle b) {
            Debug.Assert(a.IsGroup, "Inconsistency in overlapping group enumeration");
            if ((a == b) || OverlapPairAlreadyFound(a, b)) {
                return;
            }

            bool curvesIntersect = ObstaclesIntersect(a, b, out bool aIsInsideB, out bool bIsInsideA);
            if (!curvesIntersect && !aIsInsideB && !bIsInsideA) {
                return;
            }

            if (a.IsRectangle && b.IsRectangle) {
                // If these are already rectangles, we don't need to do anything here.  Non-group VisibilityPolylines
                // will not change by the group operations; we'll just grow the group if needed (if it is already 
                // nonrectangular, either because it came in that way or because it has intersected a non-rectangle).
                // However, SparseVg needs to know about the overlap so it will create interior scansegments if the
                // obstacle is not otherwise overlapped.
                if (!b.IsGroup) {
                    if (aIsInsideB || FirstRectangleContainsACornerOfTheOther(b.VisibilityBoundingBox, a.VisibilityBoundingBox)) {
                        b.OverlapsGroupCorner = true;
                    }
                }
                return;
            }

            if (!curvesIntersect) {
                // If the borders don't intersect, we don't need to do anything if both are groups or the
                // obstacle or convex hull is inside the group.  Otherwise we have to grow group a to encompass b.
                if (b.IsGroup || bIsInsideA) {
                    return;
                }
            }
            overlapPairs.Insert(new IntPair(a.Ordinal, b.Ordinal));
        }

        private static bool FirstRectangleContainsACornerOfTheOther(Rectangle a, Rectangle b) => a.Contains(b.LeftBottom) || a.Contains(b.LeftTop) || a.Contains(b.RightTop) || a.Contains(b.RightBottom);

        private static bool FirstPolylineStartIsInsideSecondPolyline(Polyline first, Polyline second) => Curve.PointRelativeToCurveLocation(first.Start, second) != PointLocation.Outside;

        private void AddClumpToConvexHull(Obstacle obstacle) {
            if (obstacle.IsOverlapped) {
                foreach (Obstacle? sibling in obstacle.Clump.Where(sib => sib.Ordinal != obstacle.Ordinal)) {
                    overlapPairs.Insert(new IntPair(obstacle.Ordinal, sibling.Ordinal));
                }

                // Clear this now so any overlaps with other obstacles in the clump won't doubly insert.
                obstacle.Clump.Clear();
            }
        }

        private void AddConvexHullToConvexHull(Obstacle obstacle) {
            if (obstacle.IsInConvexHull) {
                foreach (Obstacle? sibling in obstacle.ConvexHull.Obstacles.Where(sib => sib.Ordinal != obstacle.Ordinal)) {
                    overlapPairs.Insert(new IntPair(obstacle.Ordinal, sibling.Ordinal));
                }

                // Clear this now so any overlaps with other obstacles in the ConvexHull won't doubly insert.
                obstacle.ConvexHull.Obstacles.Clear();
            }
        }

        private void CreateClumps() {
            BasicGraph<IntPair> graph = new BasicGraph<IntPair>(overlapPairs);
            IEnumerable<IEnumerable<int>> connectedComponents = ConnectedComponentCalculator<IntPair>.GetComponents(graph);
            foreach (IEnumerable<int> component in connectedComponents) {
                // GetComponents returns at least one self-entry for each index - including the < FirstNonSentinelOrdinal ones.
                if (component.Count() == 1) {
                    continue;
                }
                Clump clump = new Clump(component.Select(OrdinalToObstacle));
                foreach (Obstacle obstacle in clump) {
                    obstacle.Clump = clump;
                }
            }
        }

        private bool CreateConvexHulls() {
            bool found = false;
            BasicGraph<IntPair> graph = new BasicGraph<IntPair>(overlapPairs);
            IEnumerable<IEnumerable<int>> connectedComponents = ConnectedComponentCalculator<IntPair>.GetComponents(graph);
            foreach (IEnumerable<int> component in connectedComponents) {
                // GetComponents returns at least one self-entry for each index - including the < FirstNonSentinelOrdinal ones.
                if (component.Count() == 1) {
                    continue;
                }
                found = true;
                IEnumerable<Obstacle> obstacles = component.Select(OrdinalToObstacle);
                IEnumerable<Point> points = obstacles.SelectMany(obs => obs.VisibilityPolyline);
                OverlapConvexHull och = new OverlapConvexHull(ConvexHull.CreateConvexHullAsClosedPolyline(points), obstacles);
                foreach (Obstacle? obstacle in obstacles) {
                    obstacle.SetConvexHull(och);
                }
            }
            return found;
        }

        private bool GrowGroupsToResolveOverlaps() {
            // This is one-at-a-time so not terribly efficient but there should be a very small number of such overlaps, if any.
            bool found = false;
            foreach (IntPair pair in overlapPairs) {
                found = true;
                Obstacle a = OrdinalToObstacle(pair.First);
                Obstacle b = OrdinalToObstacle(pair.Second);
                if (!ResolveGroupAndGroupOverlap(a, b)) {
                    ResolveGroupAndObstacleOverlap(a, b);
                }
            }
            overlapPairs.Clear();
            return found;
        }

        private static bool ResolveGroupAndGroupOverlap(Obstacle a, Obstacle b) {
            // For simplicity, pick the larger group and make grow its convex hull to encompass the smaller.
            if (!b.IsGroup) {
                return false;
            }
            if (a.VisibilityPolyline.BoundingBox.Area > b.VisibilityPolyline.BoundingBox.Area) {
                ResolveGroupAndObstacleOverlap(a, b);
            } else {
                ResolveGroupAndObstacleOverlap(b, a);
            }
            return true;
        }

        private static void ResolveGroupAndObstacleOverlap(Obstacle group, Obstacle obstacle) {
            // Create a convex hull for the group which goes outside the obstacle (which may also be a group).
            // It must go outside the obstacle so we don't have coinciding angled sides in the scanline.
            Polyline loosePolyline = obstacle.LooseVisibilityPolyline;
            GrowGroupAroundLoosePolyline(group, loosePolyline);

            // Due to rounding we may still report this to be close or intersecting; grow it again if so.
            while (ObstaclesIntersect(obstacle, group, out bool aIsInsideB, out bool bIsInsideA) || !aIsInsideB)
            {
                loosePolyline = Obstacle.CreateLoosePolyline(loosePolyline);
                GrowGroupAroundLoosePolyline(group, loosePolyline);
            }
            return;
        }

        private static void GrowGroupAroundLoosePolyline(Obstacle group, Polyline loosePolyline) {
            IEnumerable<Point> points = group.VisibilityPolyline.Select(p => p).Concat(loosePolyline.Select(p => p));
            group.SetConvexHull(new OverlapConvexHull(ConvexHull.CreateConvexHullAsClosedPolyline(points), new[] { group }));
        }

        internal static bool ObstaclesIntersect(Obstacle a, Obstacle b, out bool aIsInsideB, out bool bIsInsideA) {
            if (Curve.CurvesIntersect(a.VisibilityPolyline, b.VisibilityPolyline)) {
                aIsInsideB = false;
                bIsInsideA = false;
                return true;
            }

            aIsInsideB = FirstPolylineStartIsInsideSecondPolyline(a.VisibilityPolyline, b.VisibilityPolyline);
            bIsInsideA = !aIsInsideB && FirstPolylineStartIsInsideSecondPolyline(b.VisibilityPolyline, a.VisibilityPolyline);
            if (a.IsRectangle && b.IsRectangle) {
                // Rectangles do not require further evaluation.
                return false;
            }
            if (ObstaclesAreCloseEnoughToBeConsideredTouching(a, b, aIsInsideB, bIsInsideA)) {
                aIsInsideB = false;
                bIsInsideA = false;
                return true;
            }

            return false;
        }

        private static bool ObstaclesAreCloseEnoughToBeConsideredTouching(Obstacle a, Obstacle b, bool aIsInsideB, bool bIsInsideA) {
            // This is only called when the obstacle.VisibilityPolylines don't intersect, thus one is inside the other
            // or both are outside. If both are outside then either one's LooseVisibilityPolyline may be used.
            if (!aIsInsideB && !bIsInsideA) {
                return Curve.CurvesIntersect(a.LooseVisibilityPolyline, b.VisibilityPolyline);
            }

            // Otherwise see if the inner one is close enough to the outer border to consider them touching.
            Polyline innerLoosePolyline = aIsInsideB ? a.LooseVisibilityPolyline : b.LooseVisibilityPolyline;
            Polyline outerPolyline = aIsInsideB ? b.VisibilityPolyline : a.VisibilityPolyline;
            foreach (Point innerPoint in innerLoosePolyline) {
                if (Curve.PointRelativeToCurveLocation(innerPoint, outerPolyline) == PointLocation.Outside) {
                    Point outerParamPoint = Curve.ClosestPoint(outerPolyline, innerPoint);
                    if (!ApproximateComparer.CloseIntersections(innerPoint, outerParamPoint)) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Add ancestors that are spatial parents - they may not be in the hierarchy, but we need to be
        /// able to cross their boundaries if we're routing between obstacles on different sides of them.
        /// </summary>
        internal bool AdjustSpatialAncestors() {
            if (SpatialAncestorsAdjusted) {
                return false;
            }

            // Add each group to the AncestorSet of any spatial children (duplicate Insert() is ignored).
            foreach (Obstacle group in GetAllGroups()) {
                Rectangle groupBox = group.VisibilityBoundingBox;

                foreach (Obstacle obstacle in Root.GetNodeItemsIntersectingRectangle(groupBox)) {
                    if ((obstacle != group) && Curve.ClosedCurveInteriorsIntersect(obstacle.VisibilityPolyline, group.VisibilityPolyline)) {
                        if (obstacle.IsInConvexHull) 
                        {
                            Debug.Assert(obstacle.IsPrimaryObstacle, "Only primary obstacles should be in the hierarchy");
                            foreach (Obstacle sibling in obstacle.ConvexHull.Obstacles) 
                            {
                                AncestorSets[sibling.InputShape].Insert(group.InputShape);
                            }
                        }
                        AncestorSets[obstacle.InputShape].Insert(group.InputShape);
                    }
                }
            }

            // Remove any hierarchical ancestors that are not spatial ancestors.  Otherwise, when trying to route to
            // obstacles that *are* spatial children of such a non-spatial-but-hierarchical ancestor, we won't enable
            // crossing the boundary the first time and will always go to the full "activate all groups" path.  By
            // removing them here we not only get a better graph (avoiding some spurious crossings) but we're faster
            // both in path generation and Nudging.
            List<Shape> nonSpatialGroups = new List<Shape>();
            foreach (Obstacle child in Root.GetAllLeaves()) {
                Rectangle childBox = child.VisibilityBoundingBox;

                // This has to be two steps because we can't modify the Set during enumeration.
                nonSpatialGroups.AddRange(AncestorSets[child.InputShape].Where(anc => !childBox.Intersects(shapeIdToObstacleMap[anc].VisibilityBoundingBox)));
                foreach (Shape group in nonSpatialGroups) {
                    AncestorSets[child.InputShape].Remove(group);
                }
                nonSpatialGroups.Clear();
            }

            SpatialAncestorsAdjusted = true;
            return true;
        }

        internal IEnumerable<Obstacle> GetAllGroups() => GetAllObstacles().Where(obs => obs.IsGroup);

        /// <summary>
        /// Clear the internal state.
        /// </summary>
        internal void Clear() {
            Root = null;
            AncestorSets = null;
        }

        /// <summary>
        /// Create a LineSegment that contains the max visibility from startPoint in the desired direction.
        /// </summary>
        internal LineSegment CreateMaxVisibilitySegment(Point startPoint, Directions dir, out PointAndCrossingsList pacList) {
            Point graphBoxBorderIntersect = StaticGraphUtility.RectangleBorderIntersect(GraphBox, startPoint, dir);
            if (PointComparer.GetDirections(startPoint, graphBoxBorderIntersect) == Directions. None) {
                pacList = null;
                return new LineSegment(startPoint, startPoint);
            }
            LineSegment segment = RestrictSegmentWithObstacles(startPoint, graphBoxBorderIntersect);

            // Store this off before other operations which overwrite it.
            pacList = CurrentGroupBoundaryCrossingMap.GetOrderedListBetween(segment.Start, segment.End);
            return segment;
        }


        /// <summary>
        /// Convenience functions that call through to RectangleNode.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<Obstacle> GetAllObstacles() => allObstacles;

        /// <summary>
        /// Returns a list of all primary obstacles - secondary obstacles inside a convex hull are not needed in the VisibilityGraphGenerator.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<Obstacle> GetAllPrimaryObstacles() => Root.GetAllLeaves();

        // Hit-testing.
        internal bool IntersectionIsInsideAnotherObstacle(Obstacle sideObstacle, Obstacle eventObstacle, Point intersect, ScanDirection scanDirection) {
            insideHitTestIgnoreObstacle1 = eventObstacle;
            insideHitTestIgnoreObstacle2 = sideObstacle;
            insideHitTestScanDirection = scanDirection;
            RectangleNode<Obstacle> obstacleNode = Root.FirstHitNode(intersect, InsideObstacleHitTest);
            return (null != obstacleNode);
        }

        internal bool PointIsInsideAnObstacle(Point intersect, Directions direction) => PointIsInsideAnObstacle(intersect, ScanDirection.GetInstance(direction));

        internal bool PointIsInsideAnObstacle(Point intersect, ScanDirection scanDirection) {
            insideHitTestIgnoreObstacle1 = null;
            insideHitTestIgnoreObstacle2 = null;
            insideHitTestScanDirection = scanDirection;
            RectangleNode<Obstacle> obstacleNode = Root.FirstHitNode(intersect, InsideObstacleHitTest);
            return (null != obstacleNode);
        }

        // Ignore one (always) or both (depending on location) of these obstacles on Obstacle hit testing.
        Obstacle insideHitTestIgnoreObstacle1;
        Obstacle insideHitTestIgnoreObstacle2;
        ScanDirection insideHitTestScanDirection;

        HitTestBehavior InsideObstacleHitTest(Point location, Obstacle obstacle) {
            if ((obstacle == insideHitTestIgnoreObstacle1) || (obstacle == insideHitTestIgnoreObstacle2)) {
                // It's one of the two obstacles we already know about.
                return HitTestBehavior.Continue;
            }

            if (obstacle.IsGroup) {
                // Groups are handled differently from overlaps; we create ScanSegments (overlapped
                // if within a non-group obstacle, else non-overlapped), and turn on/off access across
                // the Group boundary vertices.
                return HitTestBehavior.Continue;
            }

            if (!StaticGraphUtility.PointIsInRectangleInterior(location, obstacle.VisibilityBoundingBox)) {
                // The point is on the obstacle boundary, not inside it.
                return HitTestBehavior.Continue;
            }

            // Note: There are rounding issues using Curve.PointRelativeToCurveLocation at angled
            // obstacle boundaries, hence this function.
            Point high = StaticGraphUtility.RectangleBorderIntersect(obstacle.VisibilityBoundingBox, location
                                , insideHitTestScanDirection.Direction)
                        + insideHitTestScanDirection.DirectionAsPoint;
            Point low = StaticGraphUtility.RectangleBorderIntersect(obstacle.VisibilityBoundingBox, location
                                , insideHitTestScanDirection.OppositeDirection)
                        - insideHitTestScanDirection.DirectionAsPoint;
            LineSegment testSeg = new LineSegment(low, high);
            IList<IntersectionInfo> xxs = Curve.GetAllIntersections(testSeg, obstacle.VisibilityPolyline, true /*liftIntersections*/);

            // If this is an extreme point it can have one intersection, in which case we're either on the border
            // or outside; if it's a collinear flat boundary, there can be 3 intersections to this point which again
            // means we're on the border (and 3 shouldn't happen anymore with the curve intersection fixes and 
            // PointIsInsideRectangle check above).  So the interesting case is that we have 2 intersections.
            if (2 == xxs.Count) {
                Point firstInt = SpliceUtility.RawIntersection(xxs[0], location);
                Point secondInt = SpliceUtility.RawIntersection(xxs[1], location);

                // If we're on either intersection, we're on the border rather than inside.
                if (!PointComparer.Equal(location, firstInt) && !PointComparer.Equal(location, secondInt)
                            && (location.CompareTo(firstInt) != location.CompareTo(secondInt))) {
                    // We're inside.  However, this may be an almost-flat side, in which case rounding
                    // could have reported the intersection with the start or end of the same side and
                    // a point somewhere on the interior of that side.  Therefore if both intersections
                    // are on the same side (integral portion of the parameter), we consider location 
                    // to be on the border.  testSeg is always xxs[*].Segment0.
                    Debug.Assert(testSeg == xxs[0].Segment0, "incorrect parameter ordering to GetAllIntersections");
                    if (!ApproximateComparer.Close(Math.Floor(xxs[0].Par1), Math.Floor(xxs[1].Par1))) {
                        return HitTestBehavior.Stop;
                    }
                }
            }
            return HitTestBehavior.Continue;
        }

        internal bool SegmentCrossesAnObstacle(Point startPoint, Point endPoint) {
            stopAtGroups = true;
            wantGroupCrossings = false;
            LineSegment obstacleIntersectSeg = RestrictSegmentPrivate(startPoint, endPoint);
            return !PointComparer.Equal(obstacleIntersectSeg.End, endPoint);
        }

#if DEBUG
        internal bool SegmentCrossesANonGroupObstacle(Point startPoint, Point endPoint) {
            stopAtGroups = false;
            wantGroupCrossings = false;
            LineSegment obstacleIntersectSeg = RestrictSegmentPrivate(startPoint, endPoint);
            return !PointComparer.Equal(obstacleIntersectSeg.End, endPoint);
        }
#endif // DEBUG

        internal LineSegment RestrictSegmentWithObstacles(Point startPoint, Point endPoint) {
            stopAtGroups = false;
            wantGroupCrossings = true;
            return RestrictSegmentPrivate(startPoint, endPoint);
        }

        private LineSegment RestrictSegmentPrivate(Point startPoint, Point endPoint) {
            GetRestrictedIntersectionTestSegment(startPoint, endPoint);
            currentRestrictedRay = new LineSegment(startPoint, endPoint);
            restrictedRayLengthSquared = (startPoint - endPoint).LengthSquared;
            CurrentGroupBoundaryCrossingMap.Clear();
            RecurseRestrictRayWithObstacles(Root);
            return currentRestrictedRay;
        }

        private void GetRestrictedIntersectionTestSegment(Point startPoint, Point endPoint) {
            // Due to rounding issues use a larger line span for intersection calculations.
            Directions segDir = PointComparer.GetPureDirection(startPoint, endPoint);
            double startX = (Directions.West == segDir) ? GraphBox.Right : ((Directions.East == segDir) ? GraphBox.Left : startPoint.X);
            double endX = (Directions.West == segDir) ? GraphBox.Left : ((Directions.East == segDir) ? GraphBox.Right : endPoint.X);
            double startY = (Directions.South == segDir) ? GraphBox.Top * 2: ((Directions.North == segDir) ? GraphBox.Bottom : startPoint.Y);
            double endY = (Directions.South == segDir) ? GraphBox.Bottom : ((Directions.North == segDir) ? GraphBox.Top : startPoint.Y);
            restrictedIntersectionTestSegment = new LineSegment(new Point(startX, startY), new Point(endX, endY));
        }

        // Due to rounding at the endpoints of the segment on intersection calculations, we need to preserve the original full-length segment.
        LineSegment restrictedIntersectionTestSegment;
        LineSegment currentRestrictedRay;
        bool wantGroupCrossings;
        bool stopAtGroups;
        double restrictedRayLengthSquared;

        private void RecurseRestrictRayWithObstacles(RectangleNode<Obstacle> rectNode) {
            // A lineSeg that moves along the boundary of an obstacle is not blocked by it.
            if (!StaticGraphUtility.RectangleInteriorsIntersect(currentRestrictedRay.BoundingBox, rectNode.Rectangle)) {
                return;
            }

            Obstacle obstacle = rectNode.UserData;
            if (null != obstacle) {
                // Leaf node. Get the interior intersections.  Use the full-length original segment for the intersection calculation.
                IList<IntersectionInfo> intersections = Curve.GetAllIntersections(restrictedIntersectionTestSegment, obstacle.VisibilityPolyline, true /*liftIntersections*/);

                if (!obstacle.IsGroup || stopAtGroups) {
                    LookForCloserNonGroupIntersectionToRestrictRay(intersections);
                    return;
                }

                if (wantGroupCrossings) {
                    AddGroupIntersectionsToRestrictedRay(obstacle, intersections);
                }

                Debug.Assert(rectNode.IsLeaf, "RectNode with UserData is not a Leaf");
                return;
            }

            // Not a leaf; recurse into children.
            RecurseRestrictRayWithObstacles(rectNode.Left);
            RecurseRestrictRayWithObstacles(rectNode.Right);
        }

        private void LookForCloserNonGroupIntersectionToRestrictRay(IList<IntersectionInfo> intersections) {
            int numberOfGoodIntersections = 0;
            IntersectionInfo closestIntersectionInfo = null;
            double localLeastDistSquared = restrictedRayLengthSquared;
            Directions testDirection = PointComparer.GetDirections(restrictedIntersectionTestSegment.Start, restrictedIntersectionTestSegment.End);
            foreach (IntersectionInfo intersectionInfo in intersections) {
                Point intersect = SpliceUtility.RawIntersection(intersectionInfo, currentRestrictedRay.Start);
                Directions dirToIntersect = PointComparer.GetDirections(currentRestrictedRay.Start, intersect);

                if (dirToIntersect == CompassVector.OppositeDir(testDirection)) {
                    continue;
                }
                ++numberOfGoodIntersections;
                
                if (Directions. None == dirToIntersect) {
                    localLeastDistSquared = 0.0;
                    closestIntersectionInfo = intersectionInfo;
                    continue;
                }

                double distSquared = (intersect - currentRestrictedRay.Start).LengthSquared;
                if (distSquared < localLeastDistSquared) {
                    // Rounding may falsely report two intersections as different when they are actually "Close",
                    // e.g. a horizontal vs. vertical intersection on a slanted edge.
                    double rawDistSquared = (intersectionInfo.IntersectionPoint - currentRestrictedRay.Start).LengthSquared;
                    if (rawDistSquared < ApproximateComparer.SquareOfDistanceEpsilon) {
                        continue;
                    } 
                    localLeastDistSquared = distSquared;
                    closestIntersectionInfo = intersectionInfo;
                }
            }

            if (null != closestIntersectionInfo) {
                // If there was only one intersection and it is quite close to an end, ignore it.
                // If there is more than one intersection, we have crossed the obstacle so we want it.
                if (numberOfGoodIntersections == 1) {
                    Point intersect = SpliceUtility.RawIntersection(closestIntersectionInfo, currentRestrictedRay.Start);
                    if (ApproximateComparer.CloseIntersections(intersect, currentRestrictedRay.Start) ||
                            ApproximateComparer.CloseIntersections(intersect, currentRestrictedRay.End)) {
                        return;
                    }
                }
                restrictedRayLengthSquared = localLeastDistSquared;
                currentRestrictedRay.End = SpliceUtility.MungeClosestIntersectionInfo(currentRestrictedRay.Start, closestIntersectionInfo
                                                   , !StaticGraphUtility.IsVertical(currentRestrictedRay.Start, currentRestrictedRay.End));
            }
        }

        private void AddGroupIntersectionsToRestrictedRay(Obstacle obstacle, IList<IntersectionInfo> intersections) {
            // We'll let the lines punch through any intersections with groups, but track the location so we can enable/disable crossing.
            foreach (IntersectionInfo intersectionInfo in intersections) {
                Point intersect = SpliceUtility.RawIntersection(intersectionInfo, currentRestrictedRay.Start);

                // Skip intersections that are past the end of the restricted segment (though there may still be some
                // there if we shorten it later, but we'll skip them later).
                double distSquared = (intersect - currentRestrictedRay.Start).LengthSquared;
                if (distSquared > restrictedRayLengthSquared) {
                    continue;
                }

                Directions dirTowardIntersect = PointComparer.GetPureDirection(currentRestrictedRay.Start, currentRestrictedRay.End);
                Polyline polyline = (Polyline)intersectionInfo.Segment1; // this is the second arg to GetAllIntersections
                Directions dirsOfSide = CompassVector.VectorDirection(polyline.Derivative(intersectionInfo.Par1));

                // The derivative is always clockwise, so if the side contains the rightward rotation of the
                // direction from the ray origin, then we're hitting it from the inside; otherwise from the outside.
                Directions dirToInsideOfGroup = dirTowardIntersect;
                if (0 != (dirsOfSide & CompassVector.RotateRight(dirTowardIntersect))) {
                    dirToInsideOfGroup = CompassVector.OppositeDir(dirToInsideOfGroup);
                }
                CurrentGroupBoundaryCrossingMap.AddIntersection(intersect, obstacle, dirToInsideOfGroup);
            }
        }
    }
}
