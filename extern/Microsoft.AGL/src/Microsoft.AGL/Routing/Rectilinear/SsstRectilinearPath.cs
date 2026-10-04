using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.DataStructures.RBTree;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.DebugHelpers;
using Microsoft.AGL.Routing.Visibility;

//following "Orthogonal Connector Routing" of Michael Wybrow and others

namespace Microsoft.AGL.Routing.Rectilinear {
    /// <summary>
    /// single source single target rectilinear path
    /// </summary>
    internal class SsstRectilinearPath {
        internal double LengthImportance { get; set; }
        internal double BendsImportance { get; set; }

        // Only bends importance needs to be public.
        internal const double DefaultBendPenaltyAsAPercentageOfDistance = 4.0;

        private VisibilityVertexRectilinear Target { get; set; }
        private VisibilityVertexRectilinear Source { get; set; }
        private Directions EntryDirectionsToTarget { get; set; }
        private double upperBoundOnCost;
        private double sourceCostAdjustment;
        private double targetCostAdjustment;

        /// <summary>
        /// The cost of the path calculation
        /// </summary>
        private double CombinedCost(double length, double numberOfBends) => LengthImportance * length + BendsImportance * numberOfBends;

        private double TotalCostFromSourceToVertex(double length, double numberOfBends) => CombinedCost(length, numberOfBends) + sourceCostAdjustment;

        /// <summary>
        /// The priority queue for path extensions.
        /// </summary>
        private GenericBinaryHeapPriorityQueueWithTimestamp<VertexEntry> queue;

        /// <summary>
        /// The list of vertices we've visited for all paths.
        /// </summary>
        private List<VisibilityVertexRectilinear> visitedVertices;

        // For consistency and speed, path extensions impose an ordering as in the paper:  straight, right, left.  We
        // enqueue entries in the reverse order of preference so the latest timestamp will be the preferred direction.
        // Thus straight-ahead neighbors are in slot 2, right in slot 1, left in slot 0.  (If the target happens
        // to be to the Left, then the heuristic lookahead score will override the Right preference).
        private class NextNeighbor {
            internal VisibilityVertexRectilinear Vertex;
            internal double Weight;

            internal NextNeighbor() => Clear();

            internal void Set(VisibilityVertexRectilinear v, double w) {
                Vertex = v;
                Weight = w;
            }

            internal void Clear() {
                Vertex = null;
                Weight = double.NaN;
            }
        }

        /// <summary>
        /// The next neighbors to extend the path to from the current vertex.
        /// </summary>
        private readonly NextNeighbor[] nextNeighbors = new[] {new NextNeighbor(), new NextNeighbor(), new NextNeighbor() };

        public SsstRectilinearPath() {
            LengthImportance = 1.0;
            BendsImportance = 1.0;
        }

        private bool InitPath(VertexEntry[] sourceVertexEntries, VisibilityVertexRectilinear source, VisibilityVertexRectilinear target) {
            if ((source == target) || !InitEntryDirectionsAtTarget(target)) {
                return false;
            }
            Target = target;
            Source = source;
            double cost = TotalCostFromSourceToVertex(0, 0) + HeuristicDistanceFromVertexToTarget(source.Point, Directions. None);
            if (cost >= upperBoundOnCost) {
                return false;
            }

            // This path starts lower than upperBoundOnCost, so create our structures and process it.
            queue = new GenericBinaryHeapPriorityQueueWithTimestamp<VertexEntry>();
            visitedVertices = [source];

            if (sourceVertexEntries == null) {
                EnqueueInitialVerticesFromSource(cost);
            } else {
                EnqueueInitialVerticesFromSourceEntries(sourceVertexEntries);
            }
            return queue.Count > 0;
        }

        private bool InitEntryDirectionsAtTarget(VisibilityVertex vert) {
            EntryDirectionsToTarget = Directions. None;

            // This routine is only called once so don't worry about optimizing foreach.
            foreach (VisibilityEdge edge in vert.OutEdges) {
                EntryDirectionsToTarget |= CompassVector.DirectionsFromPointToPoint(edge.TargetPoint, vert.Point);
            }
            foreach (VisibilityEdge edge in vert.InEdges) {
                EntryDirectionsToTarget |= CompassVector.DirectionsFromPointToPoint(edge.SourcePoint, vert.Point);
            }
            // If this returns false then the target is isolated.
            return EntryDirectionsToTarget != Directions. None;
        }

        private static bool IsInDirs(Directions direction, Directions dirs) => direction == (direction & dirs);

        internal double MultistageAdjustedCostBound(double bestCost) =>
            // Allow an additional bend's cost for intermediate stages so we don't jump out early.
            !double.IsPositiveInfinity(bestCost) ? bestCost + BendsImportance : bestCost;

        /// <summary>
        /// estimation from below for the distance
        /// </summary>
        /// <param name="point"></param>
        /// <param name="entryDirToVertex"></param>
        /// <returns></returns>
        private double HeuristicDistanceFromVertexToTarget(Point point, Directions entryDirToVertex) {
            Point vectorToTarget = Target.Point - point;
            if (ApproximateComparer.Close(vectorToTarget.X, 0) && ApproximateComparer.Close(vectorToTarget.Y, 0)) {
                // We are at the target.
                return targetCostAdjustment;
            }
            Directions dirToTarget = CompassVector.VectorDirection(vectorToTarget);

            int numberOfBends;
            if (entryDirToVertex == Directions. None) {
                entryDirToVertex = Directions.East | Directions.North | Directions.West | Directions.South;
                numberOfBends = GetNumberOfBends(entryDirToVertex, dirToTarget);
            } else {
                numberOfBends = GetNumberOfBends(entryDirToVertex, dirToTarget);
            }
            return CombinedCost(ManhattanDistance(point, Target.Point), numberOfBends) + targetCostAdjustment;
        }

        private int GetNumberOfBends(Directions entryDirToVertex, Directions dirToTarget) => CompassVector.IsPureDirection(dirToTarget)
                                    ? GetNumberOfBendsForPureDirection(entryDirToVertex, dirToTarget)
                                    : GetBendsForNotPureDirection(dirToTarget, entryDirToVertex, EntryDirectionsToTarget);

        private int GetNumberOfBendsForPureDirection(Directions entryDirToVertex, Directions dirToTarget) {
            if ( (dirToTarget & entryDirToVertex) == dirToTarget) {
                if (IsInDirs(dirToTarget, EntryDirectionsToTarget)) {
                    return 0;
                }
                if (IsInDirs(Left(dirToTarget), EntryDirectionsToTarget) || IsInDirs(Right(dirToTarget), EntryDirectionsToTarget)) {
                    return 2;
                }
                return 4;
            }
            return GetNumberOfBendsForPureDirection(AddOneTurn[(int)entryDirToVertex], dirToTarget) + 1;
        }

        private static int GetBendsForNotPureDirection(Directions dirToTarget, Directions entryDirToVertex, Directions entryDirectionsToTarget) {
            Directions a = dirToTarget & entryDirToVertex;
            if (a == Directions. None) {
                return GetBendsForNotPureDirection(dirToTarget, AddOneTurn[(int)entryDirToVertex], entryDirectionsToTarget) + 1;
            }
            Directions b = dirToTarget & entryDirectionsToTarget;
            if (b == Directions. None) {
                return GetBendsForNotPureDirection(dirToTarget, entryDirToVertex, AddOneTurn[(int)entryDirectionsToTarget]) + 1;
            }
            return (a | b) == dirToTarget ? 1 : 2;
        }

        private static readonly Directions[] AddOneTurn=new[] {
            Directions.None,  //Directions. None-> None
            Directions.North|Directions.East|Directions.West, // 1=N -> N,E,W
            Directions.North|Directions.East|Directions.South, // 2 =E->E|N|S
            (Directions)15, // 3 =E|N->E|N|S
            Directions.East|Directions.South|Directions.West, // 4 =S->E|S|W
            (Directions)15, // 5 =E|N->E|N|S|W
            (Directions)15,          //6 - S|E
            (Directions)15,  //7
            (Directions)13,  //8=W
            (Directions)15,  //9
            (Directions)15,    //10
            (Directions)15,  //11
            (Directions)15,  //12
            (Directions)15,  //13
            (Directions)15,  //14
            (Directions)15,  //15
        };

        private static Directions Left(Directions direction) {
            switch (direction) {
                case Directions. None:
                    return Directions. None;
                case Directions.North:
                    return Directions.West;
                case Directions.East:
                    return Directions.North;
                case Directions.South:
                    return Directions.East;
                case Directions.West:
                    return Directions.South;
                default:
                    throw new ArgumentOutOfRangeException("direction");
            }
        }

        private static Directions Right(Directions direction) {
            switch (direction) {
                case Directions. None:
                    return Directions. None;
                case Directions.North:
                    return Directions.East;
                case Directions.East:
                    return Directions.South;
                case Directions.South:
                    return Directions.West;
                case Directions.West:
                    return Directions.North;
                default:
                    throw new ArgumentOutOfRangeException("direction");
            }
        }

        internal static IEnumerable<Point> RestorePath(VertexEntry entry) => RestorePath(ref entry, null);

        internal static IEnumerable<Point> RestorePath(ref VertexEntry entry, VisibilityVertex firstVertexInStage) {
            if (entry == null) {
                return null;
            }
            List<Point> list = new List<Point>();
            bool skippedCollinearEntry = false;
            Directions lastEntryDir = Directions. None;
            while (true) {
                // Reduce unnecessary AxisEdge creations in Nudger by including only bend points, not points in the middle of a segment.
                if (lastEntryDir == entry.Direction) {
                    skippedCollinearEntry = true;
                } else {
                    skippedCollinearEntry = false;
                    list.Add(entry.Vertex.Point);
                    lastEntryDir = entry.Direction;
                }

                VertexEntry previousEntry = entry.PreviousEntry;
                if ((previousEntry == null) || (entry.Vertex == firstVertexInStage)) {
                    break;
                }
                entry = previousEntry;
            }
            if (skippedCollinearEntry) {
                list.Add(entry.Vertex.Point);
            }
            list.Reverse();
            return list;
        }

        #region TEST_MSAGL
#if TEST_MSAGL
        // ReSharper disable UnusedMember.Local
        private void TestShowAllPaths(VisibilityVertex source, VertexEntry mostRecentlyExtendedPath) {
            // ReSharper restore UnusedMember.Local
            IEnumerable<DebugCurve> edges = GetAllEdgesTest(source).Select(e => (ICurve) (new LineSegment(e.SourcePoint, e.TargetPoint))).
                        Select(c => new DebugCurve(c));
            IEnumerable<DebugCurve> q = queue.Select(ent => CurveFactory.CreateDiamond(2, 2, ent.Vertex.Point)).Select(c => new DebugCurve(c));
            DebugCurve[] so = new[] {
                              new DebugCurve(1, "brown", new Ellipse(3, 3, source.Point)),
                              new DebugCurve(1, "purple", CurveFactory.CreateDiamond(4, 4, Target.Point)),
                              new DebugCurve(1, "red", CurveFactory.CreateDiamond(6, 6, mostRecentlyExtendedPath.Vertex.Point))
                          };

            List<DebugCurve> pathEdges = new List<DebugCurve>();
            List<VertexEntry> newEntries = new List<VertexEntry>();
            int count = visitedVertices.Count;
            for (int ii = 0; ii < count; ++ii) {
                VisibilityVertexRectilinear vertex = visitedVertices[ii];
                if (vertex.VertexEntries == null) {
                    continue;   // this is the source vertex
                }
                foreach (VertexEntry entry in vertex.VertexEntries) {
                    if (entry == null) {
                        continue;
                    }
                    string color = "green";
                    if (entry.PreviousEntry == mostRecentlyExtendedPath) {
                        newEntries.Add(entry);
                        color = "red";
                    } else if (!entry.IsClosed) {
                        color = "yellow";
                    }
                    pathEdges.Add(new DebugCurve(2, color, new LineSegment(entry.PreviousVertex.Point, vertex.Point)));
                }
            }
            Console.WriteLine("entry {0} seq = {1} len = {2} nbend = {3} ccost = {4} hcost = {5} iters = {6}/{7}", mostRecentlyExtendedPath,
                             lastDequeueTimestamp,
                             mostRecentlyExtendedPath.Length, mostRecentlyExtendedPath.NumberOfBends,
                             CombinedCost(mostRecentlyExtendedPath.Length, mostRecentlyExtendedPath.NumberOfBends),
                             HeuristicDistanceFromVertexToTarget(mostRecentlyExtendedPath.Vertex.Point, mostRecentlyExtendedPath.Direction),
                             currentIterations, totalIterations);
            foreach (VertexEntry newEntry in newEntries) {
                Console.WriteLine("   newEntry {0} len = {1} nbend = {2} ccost = {3} hcost = {4}", newEntry,
                                 newEntry.Length, newEntry.NumberOfBends,
                                 CombinedCost(newEntry.Length, newEntry.NumberOfBends),
                                 HeuristicDistanceFromVertexToTarget(newEntry.Vertex.Point, newEntry.Direction));
            }
            DevTraceDisplay(edges.Concat(q).Concat(so).Concat(pathEdges));
        }

        // ReSharper disable UnusedMember.Local
        private void TestShowPath(VisibilityVertex source, IEnumerable<Point> pathPoints, double cost, double length, int numberOfBends) {
            // ReSharper restore UnusedMember.Local
            IEnumerable<DebugCurve> edges = GetAllEdgesTest(source).Select(e => (ICurve) (new LineSegment(e.SourcePoint, e.TargetPoint))).
                        Select(c => new DebugCurve(c));
            DebugCurve[] so = new[] {
                              new DebugCurve(1, "brown", new Ellipse(3, 3, source.Point)),
                              new DebugCurve(1, "purple", CurveFactory.CreateDiamond(4, 4, Target.Point)),
                          };

            List<DebugCurve> pathEdges = GetPathEdgeDebugCurves(pathPoints, "green");
            Console.WriteLine("path {0} -> {1} cost = {2} len = {3} nbend = {4} iters = {5}/{6}",
                            source.Point, Target.Point, cost, length, numberOfBends, currentIterations, totalIterations);
            DevTraceDisplay(edges.Concat(so).Concat(pathEdges));
        }

        internal static List<DebugCurve> GetPathEdgeDebugCurves(IEnumerable<Point> pathPoints, string color) {
            List<DebugCurve> pathEdges = new List<DebugCurve>();
            if (pathPoints != null) {
                bool first = true;
                Point prevPoint = new Point();
                foreach (Point point in pathPoints) {
                    if (first) {
                        prevPoint = point;
                        first = false;
                        continue;
                    }
                    pathEdges.Add(new DebugCurve(2, color, new LineSegment(prevPoint, point)));
                    prevPoint = point;
                }
            }
            return pathEdges;
        }

        private IEnumerable<VisibilityEdge> GetAllEdgesTest(VisibilityVertex vertex) {
            Set<VisibilityVertex> processedVertices = new Set<VisibilityVertex>();
            Queue<VisibilityVertex> q = new Queue<VisibilityVertex>();
            q.Enqueue(vertex);

            // Target may not be connected to source.
            bool targetFound = (vertex == Target);
            while (!targetFound) {
                while (q.Count > 0) {
                    VisibilityVertex v = q.Dequeue();
                    if (processedVertices.Contains(v)) {
                        continue;
                    }
                    targetFound |= (v == Target);
                    processedVertices.Insert(v);
                    foreach (VisibilityVertex u in VertsToGo(v).Where(u => !processedVertices.Contains(u))) {
                        q.Enqueue(u);
                    }
                    foreach (VisibilityEdge edge in v.OutEdges) {
                        yield return edge;
                    }
                }
                q.Enqueue(Target);
                targetFound = true;
            }
        }

        private static IEnumerable<VisibilityVertex> VertsToGo(VisibilityVertex visibilityVertex) {
            foreach (VisibilityEdge edge in visibilityVertex.OutEdges) {
                yield return edge.Target;
            }
            foreach (VisibilityEdge edge in visibilityVertex.InEdges) {
                yield return edge.Source;}
        }

#endif // TEST_MSAGL
        #endregion // TEST_MSAGL

        private void QueueReversedEntryToNeighborVertexIfNeeded(VertexEntry bestEntry, VertexEntry entryFromNeighbor, double weight) {
            // If we have a lower-cost path from bestEntry to entryFromNeighbor.PreviousVertex than the cost of entryFromNeighbor,
            // or bestEntry has degree 1 (it is a dead-end), enqueue a path in the opposite direction (entryFromNeighbor will probably
            // never be extended from this point).
            VisibilityVertexRectilinear neigVer = entryFromNeighbor.PreviousVertex;
            Directions dirToNeighbor = GetLengthAndNumberOfBendsToNeighborVertex(bestEntry, neigVer, weight, out int numberOfBends, out double length);
            if ((CombinedCost(length, numberOfBends) < CombinedCost(entryFromNeighbor.Length, entryFromNeighbor.NumberOfBends))
                    || (bestEntry.Vertex.Degree == 1)) {
                double cost = TotalCostFromSourceToVertex(length, numberOfBends) + HeuristicDistanceFromVertexToTarget(neigVer.Point, dirToNeighbor);
                EnqueueEntry(bestEntry, neigVer, length, numberOfBends, cost);
            }
        }
        
        private void UpdateEntryToNeighborVertexIfNeeded(VertexEntry bestEntry, VertexEntry neigEntry, double weight) {
            Directions dirToNeighbor = GetLengthAndNumberOfBendsToNeighborVertex(bestEntry, neigEntry.Vertex, weight, out int numberOfBends, out double length);
            if (CombinedCost(length, numberOfBends) < CombinedCost(neigEntry.Length, neigEntry.NumberOfBends)) {
                double newCost = TotalCostFromSourceToVertex(length, numberOfBends) + HeuristicDistanceFromVertexToTarget(neigEntry.Vertex.Point, dirToNeighbor);
                neigEntry.ResetEntry(bestEntry, length, numberOfBends, newCost);
                queue.DecreasePriority(neigEntry, newCost);
            }
        }

        private void CreateAndEnqueueEntryToNeighborVertex(VertexEntry bestEntry, VisibilityVertexRectilinear neigVer, double weight) {
            Directions dirToNeighbor = GetLengthAndNumberOfBendsToNeighborVertex(bestEntry, neigVer, weight, out int numberOfBends, out double length);
            double cost = TotalCostFromSourceToVertex(length, numberOfBends) + HeuristicDistanceFromVertexToTarget(neigVer.Point, dirToNeighbor);
            if (cost < upperBoundOnCost) {
                if (neigVer.VertexEntries == null) {
                    visitedVertices.Add(neigVer);
                }
                EnqueueEntry(bestEntry, neigVer, length, numberOfBends, cost);
            }
        }

        private void EnqueueEntry(VertexEntry bestEntry, VisibilityVertexRectilinear neigVer, double length, int numberOfBends, double cost) {
            VertexEntry entry = new VertexEntry(neigVer, bestEntry, length, numberOfBends, cost);
            neigVer.SetVertexEntry(entry);
            queue.Enqueue(entry, entry.Cost);
        }

        private static Directions GetLengthAndNumberOfBendsToNeighborVertex(VertexEntry prevEntry, 
                    VisibilityVertex vertex, double weight, out int numberOfBends, out double length) {
            length = prevEntry.Length + ManhattanDistance(prevEntry.Vertex.Point, vertex.Point)*weight;
            Directions directionToVertex = CompassVector.PureDirectionFromPointToPoint(prevEntry.Vertex.Point, vertex.Point);
            numberOfBends = prevEntry.NumberOfBends;
            if (prevEntry.Direction != Directions. None && directionToVertex != prevEntry.Direction) {
                numberOfBends++;
            }
            return directionToVertex;
        }

        internal static double ManhattanDistance(Point a, Point b) => Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);

        internal VertexEntry GetPathWithCost(VertexEntry[] sourceVertexEntries, VisibilityVertexRectilinear source, double adjustmentToSourceCost,
                                             VertexEntry[] targetVertexEntries, VisibilityVertexRectilinear target, double adjustmentToTargetCost, 
                                             double priorBestCost) {
            upperBoundOnCost = priorBestCost;
            sourceCostAdjustment = adjustmentToSourceCost;
            targetCostAdjustment = adjustmentToTargetCost;

            DevTracePrintSourceAndTarget(source, target);
            if (!InitPath(sourceVertexEntries, source, target)) {
                DevTraceShowPath(source, null);
                return null;
            }
#if TEST_MSAGL
            DevTraceShowAllPartialPaths(source, queue.Peek());
#endif // TEST_MSAGL

            while (queue.Count > 0) {
                TestPreDequeue();
                VertexEntry bestEntry = queue.Dequeue();
                VisibilityVertexRectilinear bestVertex = bestEntry.Vertex;
                if (bestVertex == Target) {
                    DevTraceShowPath(source, bestEntry);
                    if (targetVertexEntries == null) {
                        Cleanup();
                        return bestEntry;
                    }

                    // We'll never get a duplicate entry direction here; we either relaxed the cost via UpdateEntryToNeighborIfNeeded
                    // before we dequeued it, or it was closed.  So, we simply remove the direction from the valid target entry directions
                    // and if we get to none, we're done.  We return a null path until the final stage.
                    EntryDirectionsToTarget &= ~bestEntry.Direction;
                    if (EntryDirectionsToTarget == Directions. None) {
                        Target.VertexEntries.CopyTo(targetVertexEntries, 0); 
                        Cleanup();
                        return null;
                    }
                    upperBoundOnCost = Math.Min(MultistageAdjustedCostBound(bestEntry.Cost), upperBoundOnCost);
                    continue;
                }

                // It's safe to close this after removing it from the queue.  Any updateEntryIfNeeded that changes it must come
                // while it is still on the queue; it is removed from the queue only if it has the lowest cost path, and we have
                // no negative path weights, so any other path that might try to extend to it after this cannot have a lower cost.
                bestEntry.IsClosed = true;

                // PerfNote: Array.ForEach is optimized, but don't use .Where.
                foreach (NextNeighbor bendNeighbor in nextNeighbors) {
                    bendNeighbor.Clear();
                }
                Directions preferredBendDir = Right(bestEntry.Direction);
                ExtendPathAlongInEdges(bestEntry, bestVertex.InEdges, preferredBendDir);
                ExtendPathAlongOutEdges(bestEntry, bestVertex.OutEdges, preferredBendDir);
                foreach (NextNeighbor bendNeighbor in nextNeighbors) {
                    if (bendNeighbor.Vertex != null) {
                        ExtendPathToNeighborVertex(bestEntry, bendNeighbor.Vertex, bendNeighbor.Weight);
                    }
                }
                DevTraceShowAllPartialPaths(source, bestEntry);
            }

            // Either there is no path to the target, or we have abandoned the path due to exceeding priorBestCost.
            if ((targetVertexEntries != null) && (Target.VertexEntries != null)) {
                Target.VertexEntries.CopyTo(targetVertexEntries, 0);
            }
            DevTraceShowPath(source, null);
            Cleanup();
            return null;
        }

        private void ExtendPathAlongInEdges(VertexEntry bestEntry, List<VisibilityEdge> edges, Directions preferredBendDir) {
            // Iteration is faster than foreach and much faster than .Where.
            int count = edges.Count;
            for (int ii = 0; ii < count; ++ii) {
                VisibilityEdge edge = edges[ii];
                ExtendPathAlongEdge(bestEntry, edge, true, preferredBendDir);
            }
        }

        private void ExtendPathAlongOutEdges(VertexEntry bestEntry, RbTree<VisibilityEdge> edges, Directions preferredBendDir) {
            // Avoid GetEnumerator overhead.
            RBNode<VisibilityEdge> outEdgeNode = edges.IsEmpty() ? null : edges.TreeMinimum();
            for (; outEdgeNode != null; outEdgeNode = edges.Next(outEdgeNode)) {
                ExtendPathAlongEdge(bestEntry, outEdgeNode.Item, false, preferredBendDir);
            }
        }

        private void ExtendPathAlongEdge(VertexEntry bestEntry, VisibilityEdge edge, bool isInEdges, Directions preferredBendDir) {
            if (!IsPassable(edge)) {
                return;
            }

            // This is after the initial source vertex so PreviousEntry won't be null.
            VisibilityVertexRectilinear neigVer = (VisibilityVertexRectilinear)(isInEdges ? edge.Source : edge.Target);
            if (neigVer == bestEntry.PreviousVertex) {
                // For multistage paths, the source may be a waypoint outside the graph boundaries that is collinear
                // with both the previous and next points in the path; in that case it may have only one degree.
                // For other cases, we just ignore it and the path will be abandoned.
                if ((bestEntry.Vertex.Degree > 1) || (bestEntry.Vertex != Source)) {
                    return;
                }
                ExtendPathToNeighborVertex(bestEntry, neigVer, edge.Weight);
                return;
            }

            // Enqueue in reverse order of preference per comments on NextNeighbor class.
            Directions neigDir = CompassVector.PureDirectionFromPointToPoint(bestEntry.Vertex.Point, neigVer.Point);
            NextNeighbor nextNeighbor = nextNeighbors[2];
            if (neigDir != bestEntry.Direction) {
                nextNeighbor = nextNeighbors[(neigDir == preferredBendDir) ? 1 : 0];
            }
            Debug.Assert(nextNeighbor.Vertex == null, "bend neighbor already exists");
            nextNeighbor.Set(neigVer, edge.Weight);
        }

        private void EnqueueInitialVerticesFromSource(double cost) {
            VertexEntry bestEntry = new VertexEntry(Source, null, 0, 0, cost) {
                IsClosed = true
            };

            // This routine is only called once so don't worry about optimizing foreach.where
            foreach (VisibilityEdge edge in Source.OutEdges.Where(IsPassable)) {
                ExtendPathToNeighborVertex(bestEntry, (VisibilityVertexRectilinear)edge.Target, edge.Weight);
            }
            foreach (VisibilityEdge edge in Source.InEdges.Where(IsPassable)) {
                ExtendPathToNeighborVertex(bestEntry, (VisibilityVertexRectilinear)edge.Source, edge.Weight);
            }
        }

        private void EnqueueInitialVerticesFromSourceEntries(VertexEntry[] sourceEntries) {
            foreach (VertexEntry entry in sourceEntries) {
                if (entry != null) {
                    queue.Enqueue(entry, entry.Cost);
                }
            }
        }

        private void ExtendPathToNeighborVertex(VertexEntry bestEntry, VisibilityVertexRectilinear neigVer, double weight) {
            Directions dirToNeighbor = CompassVector.PureDirectionFromPointToPoint(bestEntry.Vertex.Point, neigVer.Point);

            VertexEntry neigEntry = (neigVer.VertexEntries != null) ? neigVer.VertexEntries[CompassVector.ToIndex(dirToNeighbor)] : null;
            if (neigEntry == null) {
                if (!CreateAndEnqueueReversedEntryToNeighborVertex(bestEntry, neigVer, weight)) {
                    CreateAndEnqueueEntryToNeighborVertex(bestEntry, neigVer, weight);
                }
            } else if (!neigEntry.IsClosed) {
                UpdateEntryToNeighborVertexIfNeeded(bestEntry, neigEntry, weight);
            }
        }

        private bool CreateAndEnqueueReversedEntryToNeighborVertex(VertexEntry bestEntry, VisibilityVertexRectilinear neigVer, double weight) {
            // VertexEntries is null for the initial source. Otherwise, if there is already a path into bestEntry's vertex
            // from neigVer, we're turning back on the path; therefore we have already enqueued the neighbors of neigVer.
            // However, the path cost includes both path length to the current point and the lookahead; this means that we
            // may now be coming into the neigVer from the opposite side with an equal score to the previous entry, but
            // the new path may be going toward the target while the old one (from neigVer to bestEntry) went away from
            // the target.  So, if we score better going in the opposite direction, enqueue bestEntry->neigVer; ignore
            // neigVer->bestEntry as it probably won't be extended again.
            if (bestEntry.Vertex.VertexEntries != null) {
                Directions dirFromNeighbor = CompassVector.PureDirectionFromPointToPoint(neigVer.Point, bestEntry.Vertex.Point);
                VertexEntry entryFromNeighbor = bestEntry.Vertex.VertexEntries[CompassVector.ToIndex(dirFromNeighbor)];
                if (entryFromNeighbor != null) {
                    Debug.Assert(entryFromNeighbor.PreviousVertex == neigVer, "mismatch in turnback PreviousEntry");
                    Debug.Assert(entryFromNeighbor.PreviousEntry.IsClosed, "turnback PreviousEntry should be closed");
                    QueueReversedEntryToNeighborVertexIfNeeded(bestEntry, entryFromNeighbor, weight);
                    return true;
                }
            }
            return false;
        }

        private static bool IsPassable(VisibilityEdge edge) => edge.IsPassable == null || edge.IsPassable();

        private void Cleanup()
        {
            foreach (VisibilityVertexRectilinear v in visitedVertices) {
                v.RemoveVertexEntries();
            }
            visitedVertices.Clear();
            queue = null;
            TestClearIterations();
        }

        #region DevTrace
#if DEVTRACE
        private static readonly DevTrace ssstTrace = new DevTrace("Rectilinear_SsstTrace", "Ssst");
        private static readonly DevTrace ssstDisplay = new DevTrace("Rectilinear_SsstDisplay");
#endif // DEVTRACE

        [Conditional("DEVTRACE")]
        void DevTraceShowAllPartialPaths(VisibilityVertex source, VertexEntry mostRecentlyExtendedPath) { 
#if DEVTRACE
            if (ssstTrace.IsLevel(3))
            {
                this.TestShowAllPaths(source, mostRecentlyExtendedPath);
            }
#endif // DEVTRACE
        }

        [Conditional("DEVTRACE")]
        private void DevTraceShowPath(VisibilityVertex source, VertexEntry lastEntry) {
#if DEVTRACE
            if (ssstTrace.IsLevel(1)) {
                var pathPoints = RestorePath(lastEntry);
                if (pathPoints != null) {
                    this.TestShowPath(source, pathPoints, lastEntry.Cost, lastEntry.Length, lastEntry.NumberOfBends);
                    return;
                }
                Console.WriteLine("path abandoned; iters = {0}/{1}", this.currentIterations, totalIterations);
            }
#endif // DEVTRACE
        }

        [Conditional("DEVTRACE")]
        private void DevTracePrintSourceAndTarget(VisibilityVertex source, VisibilityVertex target) {
#if DEVTRACE
            if (ssstTrace.IsLevel(2))
            {
                // Don't use this.Target as it hasn't been set yet.
                Console.WriteLine("find path: {0} -> {1} distance {2} sourceAdjust {3} targetAdjust {4} bendPenalty {5}",
                            source.Point, target.Point, ManhattanDistance(source.Point, target.Point),
                            this.sourceCostAdjustment, this.targetCostAdjustment, this.BendsImportance);
            }
#endif // DEVTRACE
        }

#if TEST_MSAGL
        [Conditional("DEVTRACE")]
        private static void DevTraceDisplay(IEnumerable<DebugCurve> debugCurves)
        {
#if DEVTRACE
            if (!ssstDisplay.IsLevel(1))
            {
                return;
            }
#endif // DEVTRACE
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debugCurves);
        }
#endif // TEST_MSAGL

#if TEST_MSAGL
        private int currentIterations;
        private static int totalIterations;
        private ulong lastDequeueTimestamp;
#endif // TEST_MSAGL

        [Conditional("TEST_MSAGL")]
        private void TestPreDequeue() {
#if TEST_MSAGL
            ++currentIterations;
            ++totalIterations;
            lastDequeueTimestamp = queue.PeekTimestamp();
#endif // TEST_MSAGL
        }

        [Conditional("TEST_MSAGL")]
        private void TestClearIterations() {
#if TEST_MSAGL
            currentIterations = 0;
#endif // TEST_MSAGL
        }

        #endregion // DevTrace
    }
}
