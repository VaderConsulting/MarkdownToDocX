//
// SparseVisibilityGraphGenerator.cs
// MSAGL base class to create the visibility graph consisting of nlogn ScanSegment intersections for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    // Scan direction is parallel to the sweepline which moves in the perpendicular direction;
    // i.e. scan direction is "sideways" along the sweepline.  We do several passes, following Clarkson et al.,
    // "Rectilinear shortest paths through polygonal obstacles in O(n (log n)2) time" (checked into the enlistment).
    //   1.  Enumerate all obstacles and load their extreme vertex coordinate projections to the perpendicular axis.
    //   2.  Run a scanline (in each direction) that:
    //      a.  Accumulates the vertices and generates obstacle-related Steiner points.
    //      b.  Generates the ScanSegments.
    //   3.  Iterate in parallel along the ScanSegments and *VertexPoints to determine the sparse intersections
    //       by binary division, as in the paper.
    //   4.  Finally we create the VisibilityVertices and VisibilityEdges along each ScanSegment from its
    //       list of intersections.
    // Differences from the paper largely are due to the paper's creation of non-orthogonal edges along
    // obstacle sides; instead, we create orthogonal edges to the lateral sides of the obstacle's bounding
    // box. Also, we support overlapped obstacles (interior edges are weighted, as in the non-sparse
    // implementation) and groups.
    internal partial class SparseVisibilityGraphGenerator : VisibilityGraphGenerator {
        /// <summary>
        /// The points of obstacle vertices encountered on horizontal scan.
        /// </summary>
        private readonly Set<Point> horizontalVertexPoints = [];

        /// <summary>
        /// The points of obstacle vertices encountered on vertical scan.
        /// </summary>
        private readonly Set<Point> verticalVertexPoints = [];

        /// <summary>
        /// The Steiner points generated at the bounding box of obstacles.
        /// These help ensure that we can "go around" the obstacle, as with the non-orthogonal edges in the paper.
        /// </summary>
        private readonly Set<Point> boundingBoxSteinerPoints = [];

        /// <summary>
        /// Accumulates distinct vertex projections to the X axis during sweep.
        /// </summary>
        private readonly Set<double> xCoordAccumulator = [];

        /// <summary>
        /// Accumulates distinct vertex projections to the Y axis during sweep.
        /// </summary>
        private readonly Set<double> yCoordAccumulator = [];

        /// <summary>
        /// ScanSegment vector locations on the Y axis; final array after sweep.
        /// </summary>
        private ScanSegmentVector horizontalScanSegmentVector;

        /// <summary>
        /// ScanSegment vector locations on the X axis; final array after sweep.
        /// </summary>
        private ScanSegmentVector verticalScanSegmentVector;

        /// <summary>
        /// The index from a coordinate to a horizontal vector slot.
        /// </summary>
        private readonly Dictionary<double, int> horizontalCoordMap = [];

        /// <summary>
        /// The index from a point to a vertical vector slot.
        /// </summary>
        private readonly Dictionary<double, int> verticalCoordMap = [];

        /// <summary>
        /// The index from a coordinate to a vector slot on the axis we are intersecting to.
        /// </summary>
        private Dictionary<double, int> perpendicularCoordMap;

        /// <summary>
        /// The segment vector we are intersecting along.
        /// </summary>
        private ScanSegmentVector parallelSegmentVector;

        /// <summary>
        /// The segment vector we are intersecting to.
        /// </summary>
        private ScanSegmentVector perpendicularSegmentVector;

        /// <summary>
        /// The comparer for points along the horizontal or vertical axis.
        /// </summary>
        private IComparer<Point> currentAxisPointComparer;

        internal SparseVisibilityGraphGenerator()
                : base(wantReflections: false) {
        }

        internal override void Clear() {
            base.Clear();
            Cleanup();
        }

        private void Cleanup() {
            horizontalVertexPoints.Clear();
            verticalVertexPoints.Clear();
            boundingBoxSteinerPoints.Clear();
            xCoordAccumulator.Clear();
            yCoordAccumulator.Clear();
            horizontalCoordMap.Clear();
            verticalCoordMap.Clear();
        }

        /// <summary>
        /// Generate the visibility graph along which edges will be routed.
        /// </summary>
        /// <returns></returns>
        internal override void GenerateVisibilityGraph() {
            AccumulateVertexCoords();
            CreateSegmentVectorsAndPopulateCoordinateMaps();
            RunScanLineToCreateSegmentsAndBoundingBoxSteinerPoints();
            GenerateSparseIntersectionsFromVertexPoints();
            CreateScanSegmentTrees();

            HorizontalScanSegments.DevTraceVerifyVisibility();
            VerticalScanSegments.DevTraceVerifyVisibility();
            Debug_AssertGraphIsRectilinear(VisibilityGraph, ObstacleTree);

            Cleanup();
        }

        void AccumulateVertexCoords() {
            // Unlike the paper we only generate lines for extreme vertices (i.e. on the horizontal pass we
            // don't generate a horizontal vertex projection to the Y axis for a vertex that is not on the top
            // or bottom of the obstacle).  So we can just use the bounding box.
            foreach (Obstacle obstacle in ObstacleTree.GetAllObstacles()) {
                xCoordAccumulator.Insert(obstacle.VisibilityBoundingBox.Left);
                xCoordAccumulator.Insert(obstacle.VisibilityBoundingBox.Right);
                yCoordAccumulator.Insert(obstacle.VisibilityBoundingBox.Top);
                yCoordAccumulator.Insert(obstacle.VisibilityBoundingBox.Bottom);
            }
        }

        private void CreateSegmentVectorsAndPopulateCoordinateMaps() {
            horizontalScanSegmentVector = new ScanSegmentVector(yCoordAccumulator, true);
            verticalScanSegmentVector = new ScanSegmentVector(xCoordAccumulator, false);

            for (int slot = 0; slot < horizontalScanSegmentVector.Length; ++slot) {
                horizontalCoordMap[horizontalScanSegmentVector[slot].Coord] = slot;
            }
            for (int slot = 0; slot < verticalScanSegmentVector.Length; ++slot) {
                verticalCoordMap[verticalScanSegmentVector[slot].Coord] = slot;
            }
        }

        private void RunScanLineToCreateSegmentsAndBoundingBoxSteinerPoints() {
            // Do a scanline pass to create scan segments that span the entire height/width of the graph
            // (mixing overlapped with free segments as needed) and generate the type-2 Steiner points.
            base.GenerateVisibilityGraph();
            horizontalScanSegmentVector.ScanSegmentsComplete();
            verticalScanSegmentVector.ScanSegmentsComplete();
            xCoordAccumulator.Clear();
            yCoordAccumulator.Clear();
        }

        internal override void InitializeEventQueue(ScanDirection scanDir)
        {
            base.InitializeEventQueue(scanDir);
            SetVectorsAndCoordMaps(scanDir);
            AddAxisCoordinateEvents(scanDir);
        }

        private void AddAxisCoordinateEvents(ScanDirection scanDir) {
            // Normal event ordering will apply - and will thus order the ScanSegments created in the vectors.
            if (scanDir.IsHorizontal) {
                foreach (double coord in yCoordAccumulator) {
                    base.eventQueue.Enqueue(new AxisCoordinateEvent(new Point(ObstacleTree.GraphBox.Left - SentinelOffset, coord)));
                }
                return;
            }
            foreach (double coord in xCoordAccumulator) {
                base.eventQueue.Enqueue(new AxisCoordinateEvent(new Point(coord, ObstacleTree.GraphBox.Bottom - SentinelOffset)));
            }
        }

        protected override void ProcessCustomEvent(SweepEvent evt) {
            if (!ProcessAxisCoordinate(evt)) {
                base.ProcessCustomEvent(evt);
            }
        }

        private bool ProcessAxisCoordinate(SweepEvent evt) {
            if (evt is AxisCoordinateEvent axisEvent)
            {
                CreateScanSegmentsOnAxisCoordinate(axisEvent.Site);
                return true;
            }
            return false;
        }

        protected override bool InsertPerpendicularReflectionSegment(Point start, Point end) {
            Debug.Assert(false, "base.wantReflections is false in Sparse mode so this should never be called");
// ReSharper disable HeuristicUnreachableCode
            return false;
// ReSharper restore HeuristicUnreachableCode
        }

        protected override bool InsertParallelReflectionSegment(Point start, Point end, Obstacle eventObstacle,
                BasicObstacleSide lowNborSide, BasicObstacleSide highNborSide, BasicReflectionEvent action) {
            Debug.Assert(false, "base.wantReflections is false in Sparse mode so this should never be called");
// ReSharper disable HeuristicUnreachableCode
            return false;
// ReSharper restore HeuristicUnreachableCode
        }

        protected override void ProcessVertexEvent(RBNode<BasicObstacleSide> lowSideNode,
                                                   RBNode<BasicObstacleSide> highSideNode, BasicVertexEvent vertexEvent) {
            Set<Point> vertexPoints = (base.ScanDirection.IsHorizontal) ? horizontalVertexPoints : verticalVertexPoints;
            vertexPoints.Insert(vertexEvent.Site);

            // For easier reading...
            BasicObstacleSide lowNborSide = LowNeighborSides.LowNeighbor.Item;
            BasicObstacleSide highNborSide = HighNeighborSides.HighNeighbor.Item;
            Directions highDir = base.ScanDirection.Direction;
            Directions lowDir = base.ScanDirection.OppositeDirection;

            // Generate the neighbor side intersections, regardless of overlaps; these are the type-2 Steiner points.
            Point lowSteiner = ScanLineIntersectSide(vertexEvent.Site, lowNborSide);
            Point highSteiner = ScanLineIntersectSide(vertexEvent.Site, highNborSide);

            // Add the intersections at the neighbor bounding boxes if the intersection is not at a sentinel.  
            // Go in the opposite direction from the neighbor intersection to find the border between the Steiner 
            // point and vertexEvent.Site (unless vertexEvent.Site is inside the bounding box).
            if (ObstacleTree.GraphBox.Contains(lowSteiner)) {
                Point bboxIntersectBeforeLowSteiner = StaticGraphUtility.RectangleBorderIntersect(lowNborSide.Obstacle.VisibilityBoundingBox, lowSteiner, highDir);
                if (PointComparer.IsPureLower(bboxIntersectBeforeLowSteiner, vertexEvent.Site)) {
                    boundingBoxSteinerPoints.Insert(bboxIntersectBeforeLowSteiner);
                }
            }
            if (ObstacleTree.GraphBox.Contains(highSteiner)) {
                Point bboxIntersectBeforeHighSteiner = StaticGraphUtility.RectangleBorderIntersect(highNborSide.Obstacle.VisibilityBoundingBox, highSteiner, lowDir);
                if (PointComparer.IsPureLower(vertexEvent.Site, bboxIntersectBeforeHighSteiner)) {
                    boundingBoxSteinerPoints.Insert(bboxIntersectBeforeHighSteiner);
                }
            }

            // Add the corners of the bounding box of the vertex obstacle, if they are visible to the event site.
            // This ensures that we "go around" the obstacle, as with the non-orthogonal edges in the paper.
            GetBoundingCorners(lowSideNode.Item.Obstacle.VisibilityBoundingBox, vertexEvent is OpenVertexEvent, ScanDirection.IsHorizontal,
                        out Point lowCorner, out Point highCorner);
            if (PointComparer.IsPureLower(lowSteiner, lowCorner) || lowNborSide.Obstacle.IsInSameClump(vertexEvent.Obstacle)) {
                vertexPoints.Insert(lowCorner);
            }
            if (PointComparer.IsPureLower(highCorner, highSteiner) || highNborSide.Obstacle.IsInSameClump(vertexEvent.Obstacle)) {
                vertexPoints.Insert(highCorner);
            }
        }

        private static void GetBoundingCorners(Rectangle boundingBox, bool isLowSide, bool isHorizontal, out Point lowCorner, out Point highCorner) {
            if (isLowSide) {
                lowCorner = boundingBox.LeftBottom;
                highCorner = isHorizontal ? boundingBox.RightBottom : boundingBox.LeftTop;
                return;
            }
            lowCorner = isHorizontal ? boundingBox.LeftTop : boundingBox.RightBottom;
            highCorner = boundingBox.RightTop;
        }

        private void CreateScanSegmentsOnAxisCoordinate(Point site) {
            base.CurrentGroupBoundaryCrossingMap.Clear();

            // Iterate the ScanLine and create ScanSegments.  There will always be at least the two sentinel sides.
            RBNode<BasicObstacleSide> sideNode = base.scanLine.Lowest();
            RBNode<BasicObstacleSide> nextNode = base.scanLine.NextHigh(sideNode);
            int overlapDepth = 0;
            Point start = site;
            bool isInsideOverlappedObstacle = false;
            for (; null != nextNode; nextNode = base.scanLine.NextHigh(nextNode)) {
                if (SkipSide(start, nextNode.Item)) {
                    continue;
                }

                if (nextNode.Item.Obstacle.IsGroup) {
                    // Do not create internal group crossings in non-overlapped obstacles.
                    if ((overlapDepth == 0) || isInsideOverlappedObstacle) {
                        HandleGroupCrossing(site, nextNode.Item);
                    }
                    continue;
                }

                bool isLowSide = nextNode.Item is LowObstacleSide;
                if (isLowSide) {
                    if (overlapDepth > 0) {
                        ++overlapDepth;
                        continue;
                    }

                    // We are not overlapped, so create a ScanSegment from the previous side intersection to the
                    // intersection with the side in nextNode.Item.
                    start = CreateScanSegment(start, nextNode.Item, ScanSegment.NormalWeight);
                    base.CurrentGroupBoundaryCrossingMap.Clear();
                    overlapDepth = 1;
                    isInsideOverlappedObstacle = nextNode.Item.Obstacle.IsOverlapped;
                    continue;
                }

                // This is a HighObstacleSide.  If we've got overlap nesting, decrement the depth.
                Debug.Assert(overlapDepth > 0, "Overlap depth must be positive");
                --overlapDepth;
                if (overlapDepth > 0) {
                    continue;
                }

                // If we are not within an overlapped obstacle, don't bother creating the overlapped ScanSegment
                // as there will never be visibility connecting to it.
                start = (nextNode.Item.Obstacle.IsOverlapped || nextNode.Item.Obstacle.OverlapsGroupCorner)
                        ? CreateScanSegment(start, nextNode.Item, ScanSegment.OverlappedWeight)
                        : ScanLineIntersectSide(start, nextNode.Item);
                base.CurrentGroupBoundaryCrossingMap.Clear();
                isInsideOverlappedObstacle = false;
            }

            // The final piece.
            Point end = base.ScanDirection.IsHorizontal
                    ? new Point(ObstacleTree.GraphBox.Right + SentinelOffset, start.Y)
                    : new Point(start.X, ObstacleTree.GraphBox.Top + SentinelOffset);
            parallelSegmentVector.CreateScanSegment(start, end, ScanSegment.NormalWeight,
                    base.CurrentGroupBoundaryCrossingMap.GetOrderedListBetween(start, end));
            parallelSegmentVector.ScanSegmentsCompleteForCurrentSlot();
        }

        private void HandleGroupCrossing(Point site, BasicObstacleSide groupSide) {
            if (!base.ScanLineCrossesObstacle(site, groupSide.Obstacle)) {
                return;
            }
            // Here we are always going left-to-right.  As in base.SkipToNeighbor, we don't stop traversal for groups,
            // neither do we create overlapped edges (unless we're inside a non-group obstacle).  Instead we turn
            // the boundary crossing on or off based on group membership at ShortestPath-time.  Even though this is
            // the sparse VG, we always create these edges at group boundaries so we don't skip over them.
            Directions dirToInsideOfGroup = (groupSide is LowObstacleSide) ? base.ScanDirection.Direction : base.ScanDirection.OppositeDirection;
            Point intersect = ScanLineIntersectSide(site, groupSide);
            GroupBoundaryCrossing crossing = base.CurrentGroupBoundaryCrossingMap.AddIntersection(intersect, groupSide.Obstacle, dirToInsideOfGroup);

            // The vertex crossing the edge is perpendicular to the group boundary.  A rectilinear group will also have
            // an edge parallel to that group boundary that includes the point of that crossing vertex; therefore we must
            // split that non-crossing edge at that vertex.
            AddPerpendicularCoordForGroupCrossing(intersect);

            // Similarly, the crossing edge's opposite vertex may be on a perpendicular segment.
            Point interiorPoint = crossing.GetInteriorVertexPoint(intersect);
            AddPerpendicularCoordForGroupCrossing(interiorPoint);
        }

        private void AddPerpendicularCoordForGroupCrossing(Point intersect) {
            int nonCrossingPerpSlot = FindPerpendicularSlot(intersect, 0);
            if (-1 != nonCrossingPerpSlot) {
                perpendicularSegmentVector[nonCrossingPerpSlot].AddPendingPerpendicularCoord(parallelSegmentVector.CurrentSlot.Coord);
            }
        }

        private bool SkipSide(Point start, BasicObstacleSide side) {
            if (side.Obstacle.IsSentinel) {
                return true;
            }

            // Skip sides of obstacles that we do not actually pass through.
            Rectangle bbox = side.Obstacle.VisibilityBoundingBox;
            if (base.ScanDirection.IsHorizontal) {
                return ((start.Y == bbox.Bottom) || (start.Y == bbox.Top));
            }
            return ((start.X == bbox.Left) || (start.X == bbox.Right));
        }

        private Point CreateScanSegment(Point start, BasicObstacleSide side, double weight) {
            Point end = ScanLineIntersectSide(start, side);
            if (start != end) {
                parallelSegmentVector.CreateScanSegment(start, end, weight, CurrentGroupBoundaryCrossingMap.GetOrderedListBetween(start, end));
            }
            return end;
        }

        private void GenerateSparseIntersectionsFromVertexPoints() {
            VisibilityGraph = NewVisibilityGraph();

            // Generate the sparse intersections between ScanSegments based upon the ordered vertexPoints.
            GenerateSparseIntersectionsAlongHorizontalAxis();
            GenerateSparseIntersectionsAlongVerticalAxis();

            ConnectAdjoiningScanSegments();

            // Now each segment has the coordinates all of its intersections, so create the visibility graph.
            horizontalScanSegmentVector.CreateSparseVerticesAndEdges(VisibilityGraph);
            verticalScanSegmentVector.CreateSparseVerticesAndEdges(VisibilityGraph);
        }

        private void GenerateSparseIntersectionsAlongHorizontalAxis() {
            currentAxisPointComparer = new HorizontalPointComparer();
            IEnumerator<Point> vertexPoints = horizontalVertexPoints.OrderBy(point => point, currentAxisPointComparer).GetEnumerator();
            EnumeratorWrapper<Point> bboxSteinerPoints = new EnumeratorWrapper<Point>(boundingBoxSteinerPoints.OrderBy(point => point, currentAxisPointComparer));
            base.ScanDirection = ScanDirection.HorizontalInstance;
            SetVectorsAndCoordMaps(base.ScanDirection);
            GenerateSparseIntersections(vertexPoints, bboxSteinerPoints);
        }

        private void GenerateSparseIntersectionsAlongVerticalAxis() {
            currentAxisPointComparer = new VerticalPointComparer();
            IEnumerator<Point> vertexPoints = verticalVertexPoints.OrderBy(point => point, currentAxisPointComparer).GetEnumerator();
            EnumeratorWrapper<Point> bboxSteinerPoints = new EnumeratorWrapper<Point>(boundingBoxSteinerPoints.OrderBy(point => point, currentAxisPointComparer));
            base.ScanDirection = ScanDirection.VerticalInstance;
            SetVectorsAndCoordMaps(base.ScanDirection);
            GenerateSparseIntersections(vertexPoints, bboxSteinerPoints);
        }

        private void SetVectorsAndCoordMaps(ScanDirection scanDir) {
            if (scanDir.IsHorizontal) {
                parallelSegmentVector = horizontalScanSegmentVector;
                perpendicularSegmentVector = verticalScanSegmentVector;
                perpendicularCoordMap = verticalCoordMap;
            } else {
                parallelSegmentVector = verticalScanSegmentVector;
                perpendicularSegmentVector = horizontalScanSegmentVector;
                perpendicularCoordMap = horizontalCoordMap;
            }
        }

        private void ConnectAdjoiningScanSegments() {
            // Ensure there is a vertex at the end/start point of two ScanSegments; these will always differ in overlappedness.
            horizontalScanSegmentVector.ConnectAdjoiningSegmentEndpoints();
            verticalScanSegmentVector.ConnectAdjoiningSegmentEndpoints();
        }

        internal class HorizontalPointComparer : IComparer<Point> {
            // Order by vertical first, then horizontal.
            public int Compare(Point lhs, Point rhs) {
                int cmp = lhs.Y.CompareTo(rhs.Y);
                return (0 != cmp) ? cmp : lhs.X.CompareTo(rhs.X);
            }
        }

        public class VerticalPointComparer : IComparer<Point> {
            // Order by horizontal first, then vertical.
            public int Compare(Point lhs, Point rhs) {
                int cmp = lhs.X.CompareTo(rhs.X);
                return (0 != cmp) ? cmp : lhs.Y.CompareTo(rhs.Y);
            }
        }

        private void GenerateSparseIntersections(IEnumerator<Point> vertexPoints, EnumeratorWrapper<Point> bboxSteinerPoints) {
            perpendicularSegmentVector.ResetForIntersections();
            parallelSegmentVector.ResetForIntersections();

            // Position the enumerations to the first point.
            vertexPoints.MoveNext();
            bboxSteinerPoints.MoveNext();
            foreach (ScanSegmentVectorItem item in parallelSegmentVector.Items) {
                for (;;) {
                    if (!item.CurrentSegment.ContainsPoint(vertexPoints.Current)) {
                        // Done accumulating intersections for the current segment; move to the next segment.
                        if (!AddSteinerPointsToInterveningSegments(vertexPoints.Current, bboxSteinerPoints, item)
                                || !item.TraverseToSegmentContainingPoint(vertexPoints.Current)) {
                            // Done with this vectorItem, move to the next item.
                            break;
                        }
                    }

                    AddPointsToCurrentSegmentIntersections(bboxSteinerPoints, item);
                    GenerateIntersectionsFromVertexPointForCurrentSegment(vertexPoints.Current, item);

                    if (item.PointIsCurrentEndAndNextStart(vertexPoints.Current)) {
                        // MoveNext will always return true because the test to enter this block returned true.
                        item.MoveNext();
                        Debug.Assert(item.HasCurrent, "MoveNext ended before EndAndNextStart");
                        continue;
                    }

                    if (!vertexPoints.MoveNext()) {
                        // No more vertexPoints; we're done.
                        Debug.Assert(!bboxSteinerPoints.HasCurrent, "Some boundingBoxSteinerPoints remain");
                        return;
                    }
                }
            }

            // We should have exited in the "no more vertexPoints" case above.
            Debug.Assert(false, "Mismatch in points and segments");
        }

        private bool AddSteinerPointsToInterveningSegments(Point currentVertexPoint, EnumeratorWrapper<Point> bboxSteinerPoints, ScanSegmentVectorItem item) {
            // With overlaps, we may have bboxSteinerPoints on segments that do not contain vertices.
            while (bboxSteinerPoints.HasCurrent && (currentAxisPointComparer.Compare(bboxSteinerPoints.Current, currentVertexPoint) == -1)) {
                if (!item.TraverseToSegmentContainingPoint(bboxSteinerPoints.Current)) {
                    // Done with this vectorItem, move to the next item.
                    return false;
                }
                AddPointsToCurrentSegmentIntersections(bboxSteinerPoints, item);
            }
            return true;
        }

        private void AddPointsToCurrentSegmentIntersections(EnumeratorWrapper<Point> pointsToAdd, ScanSegmentVectorItem parallelItem) {
            // The first Steiner point should be in the segment, unless we have a non-orthogonal or overlapped or both situation
            // that results in no Steiner points having been generated, or Steiner points being generated on a segment that has
            // the opposite overlap state from the segment containing the corresponding vertex.
            for (; pointsToAdd.HasCurrent && parallelItem.CurrentSegment.ContainsPoint(pointsToAdd.Current); pointsToAdd.MoveNext()) {
                int steinerSlot = FindPerpendicularSlot(pointsToAdd.Current, 0);
                AddSlotToSegmentIntersections(parallelItem, steinerSlot);
            }
        }

        private void GenerateIntersectionsFromVertexPointForCurrentSegment(Point site, ScanSegmentVectorItem parallelItem) {
            int perpStartSlot = FindPerpendicularSlot(parallelItem.CurrentSegment.Start, 1);
            int perpEndSlot = FindPerpendicularSlot(parallelItem.CurrentSegment.End, -1);
            int siteSlot = FindPerpendicularSlot(site, 0);

            // See comments in FindIntersectingSlot; we don't add non-extreme vertices in the perpendicular direction
            // so in some heavily-overlapped scenarios, we may not have any intersections within this scan segment.
            if (perpStartSlot >= perpEndSlot) {
                return;
            }

            AddSlotToSegmentIntersections(parallelItem, perpStartSlot);
            AddSlotToSegmentIntersections(parallelItem, perpEndSlot);
            if ((siteSlot > perpStartSlot) && (siteSlot < perpEndSlot)) {
                AddSlotToSegmentIntersections(parallelItem, siteSlot);
                AddBinaryDivisionSlotsToSegmentIntersections(parallelItem, perpStartSlot, siteSlot, perpEndSlot);
            }
        }

        // These are called when the site may not be in the vector.
        private int FindPerpendicularSlot(Point site, int directionIfMiss) => FindIntersectingSlot(perpendicularSegmentVector, perpendicularCoordMap, site, directionIfMiss);

        private static int FindIntersectingSlot(ScanSegmentVector segmentVector, Dictionary<double, int> coordMap, Point site, int directionIfMiss) {
            double coord = segmentVector.GetParallelCoord(site);
            if (coordMap.TryGetValue(coord, out int slot))
            {
                return slot;
            }

            // There are a few cases where the perpCoord is not in the map:
            // 1.  The first ScanSegment in a slot will have a Start at the sentinel, which is before the first 
            //     perpendicular segment; similarly, the last ScanSegment in a slot will have an out-of-range End.
            // 2.  Sequences of overlapped/nonoverlapped scan segments that pass through obstacles.  Their start
            //     and end points are not in vertexPoints because they were not vertex-derived, so we find the
            //     closest bracketing coordinates that are in the vectors.
            // 3.  Non-extreme vertices in the perpendicular direction (e.g. for a triangle, we add the X's of
            //     the left and right to the coords, but not of the top).
            // 4.  Non-rectilinear group side intersections.
            return (0 == directionIfMiss) ? -1 : segmentVector.FindNearest(coord, directionIfMiss);
        }

        private void AddSlotToSegmentIntersections(ScanSegmentVectorItem parallelItem, int perpSlot) {
            ScanSegmentVectorItem perpItem = perpendicularSegmentVector[perpSlot];
            parallelItem.CurrentSegment.AddSparseVertexCoord(perpItem.Coord);
            perpItem.AddPerpendicularCoord(parallelItem.Coord);
        }

        private void AddBinaryDivisionSlotsToSegmentIntersections(ScanSegmentVectorItem parallelItem, int startSlot, int siteSlot, int endSlot) {
            // The input parameters' slots have already been added to the segment's coords.  
            // If there was no object to the low or high side, then the start or end slot was already
            // the graphbox max (0 or perpSegmentVector.Length, respectively).  So start dividing.
            int low = 0;
            int high = perpendicularSegmentVector.Length - 1;

            // Terminate when we are one away because we don't have an edge from a point to itself.
            while ((high - low) > 1) {
                int mid = low + ((high - low) / 2);

                // We only use the half of the graph that the site is in, so arbitrarily decide that it is
                // in the lower half if it is at the midpoint.
                if (siteSlot <= mid) {
                    high = mid;
                    if ((siteSlot < high) && (high <= endSlot)) {
                        AddSlotToSegmentIntersections(parallelItem, high);
                    }
                    continue;
                }
                low = mid;
                if ((siteSlot > low) && (low >= startSlot)) {
                    AddSlotToSegmentIntersections(parallelItem, low);
                }
            }
        }

        // Create the ScanSegmentTrees that functions as indexes for port-visibility splicing.
        private void CreateScanSegmentTrees() {
            CreateScanSegmentTree(horizontalScanSegmentVector, HorizontalScanSegments);
            CreateScanSegmentTree(verticalScanSegmentVector, VerticalScanSegments);
        }

        private static void CreateScanSegmentTree(ScanSegmentVector segmentVector, ScanSegmentTree segmentTree) {
            foreach (ScanSegmentVectorItem item in segmentVector.Items) {
                for (ScanSegment segment = item.FirstSegment; segment != null; segment = segment.NextSegment) {
                    if (segment.HasVisibility()) {
                        segmentTree.InsertUnique(segment);
                    }
                }
            }
        }
    }
}