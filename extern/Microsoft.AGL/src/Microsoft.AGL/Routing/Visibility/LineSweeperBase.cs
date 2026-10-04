using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.DataStructures.RBTree;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Routing.Spline.ConeSpanner;

namespace Microsoft.AGL.Routing.Visibility {
    internal class LineSweeperBase : IComparer<SweepEvent> {
        Point directionPerp; // sweep direction rotated 90 degrees clockwse
        BinaryHeapWithComparer<SweepEvent> eventQueue;

        protected RbTree<SegmentBase> LeftObstacleSideTree { get; set; }

        protected ObstacleSideComparer ObstacleSideComparer { get; set; }

        protected RbTree<SegmentBase> RightObstacleSideTree { get; set; }
        protected Set<Point> Ports;
        public LineSweeperBase(IEnumerable<Polyline> obstacles, Point sweepDirection) {
            Obstacles = obstacles;
            SweepDirection = sweepDirection;
            DirectionPerp = sweepDirection.Rotate(-Math.PI / 2);
            EventQueue = new BinaryHeapWithComparer<SweepEvent>(this);
            ObstacleSideComparer = new ObstacleSideComparer(this);
            LeftObstacleSideTree = new RbTree<SegmentBase>(ObstacleSideComparer);
            RightObstacleSideTree = new RbTree<SegmentBase>(ObstacleSideComparer);
        }

        protected internal BinaryHeapWithComparer<SweepEvent> EventQueue { get => eventQueue; set => eventQueue = value;
        }
        public Point SweepDirection { get; set; }
        /// <summary>
        /// sweep direction rotated by 90 degrees clockwise
        /// </summary>
        protected Point DirectionPerp { get => directionPerp; set => directionPerp = value;
        }

        protected double PreviousZ=double.NegativeInfinity;
        double z;
        public double Z
        {
            get => z;
            set
            {
                if (value > z + ApproximateComparer.Tolerance)
                    PreviousZ = z;
#if DEBUGGLEE
                Debug.Assert(PreviousZ<=value); 
#endif
                z = value;
                //       Debug.Assert(TreesAreCorrect());
            }
        }

        // protected virtual bool TreesAreCorrect() { return true; }

        protected internal IEnumerable<Polyline> Obstacles { get; set; }


        protected double GetZ(SweepEvent eve) => SweepDirection * eve.Site;

        protected double GetZ(Point point) => SweepDirection * point;

        protected bool SegmentIsNotHorizontal(Point a, Point b) => Math.Abs((a - b) * SweepDirection) > ApproximateComparer.DistanceEpsilon;

        protected bool SegmentIsHorizontal(Point a, Point b) => Math.Abs((a - b) * SweepDirection) <= ApproximateComparer.DistanceEpsilon;

        protected void RemoveLeftSide(LeftObstacleSide side) {
            ObstacleSideComparer.SetOperand(side);
            LeftObstacleSideTree.Remove(side);
        }

        protected void RemoveRightSide(RightObstacleSide side) {
            ObstacleSideComparer.SetOperand(side);
            RightObstacleSideTree.Remove(side);
        }

        protected void InsertLeftSide(LeftObstacleSide side) {
            ObstacleSideComparer.SetOperand(side);
            LeftObstacleSideTree.Insert((side));
        }

        protected void InsertRightSide(RightObstacleSide side) {
            ObstacleSideComparer.SetOperand(side);
            RightObstacleSideTree.Insert(side);
        }

        protected RightObstacleSide FindFirstObstacleSideToTheLeftOfPoint(Point point) {
            RBNode<SegmentBase> node =
                RightObstacleSideTree.FindLast(
                    s => Point.PointToTheRightOfLineOrOnLine(point, s.Start, s.End));
            return node == null ? null : (RightObstacleSide)(node.Item);
        }

        protected LeftObstacleSide FindFirstObstacleSideToToTheRightOfPoint(Point point) {
            RBNode<SegmentBase> node =
                LeftObstacleSideTree.FindFirst(
                    s => !Point.PointToTheRightOfLineOrOnLine(point, s.Start, s.End));
            return node == null ? null : (LeftObstacleSide)node.Item;
        }

        protected void EnqueueEvent(SweepEvent eve) {
            Debug.Assert(GetZ(eve.Site)>=PreviousZ);
            eventQueue.Enqueue(eve);
        }

        protected void InitQueueOfEvents() {
            foreach (Polyline obstacle in Obstacles)
                EnqueueLowestPointsOnObstacles(obstacle);
            if (Ports != null)
                foreach (Point point in Ports)
                    EnqueueEvent(new PortObstacleEvent(point));
        }

        void EnqueueLowestPointsOnObstacles(Polyline poly) {
            PolylinePoint candidate = GetLowestPoint(poly);
            EnqueueEvent(new LowestVertexEvent(candidate));
        }

        PolylinePoint GetLowestPoint(Polyline poly) {
            PolylinePoint candidate = poly.StartPoint;
            PolylinePoint pp = poly.StartPoint.Next;

            for (; pp != null; pp = pp.Next)
                if (Less(pp.Point, candidate.Point))
                    candidate = pp;
            return candidate;
        }

        /// <summary>
        /// imagine that direction points up,
        /// lower events have higher priorities,
        /// for events at the same level events to the left have higher priority
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public int Compare(SweepEvent a, SweepEvent b) {
            ValidateArg.IsNotNull(a, "a");
            ValidateArg.IsNotNull(b, "b");
            Point aSite = a.Site;
            Point bSite = b.Site;
            return ComparePoints(ref aSite, ref bSite);
        }

        bool Less(Point a, Point b) => ComparePoints(ref a, ref b) < 0;

        int ComparePoints(ref Point aSite, ref Point bSite) {
            double aProjection = SweepDirection * aSite;
            double bProjection = SweepDirection * bSite;
            if (aProjection < bProjection)
                return -1;
            if (aProjection > bProjection)
                return 1;

            aProjection = directionPerp * aSite;
            bProjection = directionPerp * bSite;

            if (aProjection < bProjection)
                return -1;
            return aProjection > bProjection ? 1 : 0;
        }
    }
}
