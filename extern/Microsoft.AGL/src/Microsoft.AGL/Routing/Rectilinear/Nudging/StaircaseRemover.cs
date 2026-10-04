using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Geometry.RTree;

namespace Microsoft.AGL.Routing.Rectilinear.Nudging {
    internal class StaircaseRemover {
        protected List<Path> Paths { get; set; }

        protected RTree<Polyline> HierarchyOfObstacles { get; set; }
        readonly RTree<SegWithIndex> segTree=new RTree<SegWithIndex>();
        Set<Path> crossedOutPaths = [];

        StaircaseRemover(List<Path> paths, RectangleNode<Polyline> hierarchyOfObstacles) {
            HierarchyOfObstacles = new RTree<Polyline>(hierarchyOfObstacles);
            Paths = paths;
        }


        internal static void RemoveStaircases(List<Path> paths, RectangleNode<Polyline> hierarchyOfObstacles) {
            StaircaseRemover r = new StaircaseRemover(paths, hierarchyOfObstacles);
            r.Calculate();
        }

        void Calculate() {
            InitHierarchies();
            bool success;
            do {
                success = false;
                foreach (Path path in Paths.Where(p=>!crossedOutPaths.Contains(p)))
                    success |= ProcessPath(path);
            } while (success);
        }

        bool ProcessPath(Path path) {
            Point[] pts = (Point[])path.PathPoints;
            if (ProcessPoints(ref pts, out bool canHaveStaircase))
            {
                path.PathPoints = pts;
                return true;
            }
            if (!canHaveStaircase)
                crossedOutPaths.Insert(path);
            return false;
        }

        bool ProcessPoints(ref Point[] pts, out bool canHaveStaircase) {
            int staircaseStart  = FindStaircaseStart(pts, out canHaveStaircase);
            if (staircaseStart < 0) return false;
            pts = RemoveStaircase(pts, staircaseStart);
            return true;
        }


        int FindStaircaseStart(Point[] pts, out bool canHaveStaircase) {
            canHaveStaircase = false;
            if (pts.Length < 5) // At least five points make a staircase
                return -1;
            SegWithIndex[] segs = new[] {
                                 new SegWithIndex(pts, 0), new SegWithIndex(pts, 1), new SegWithIndex(pts, 2),
                                 new SegWithIndex(pts, 3)
                             };
            int segToReplace = 0;

            for (int i = 0;;) {
                if (IsStaircase(pts, i, segs, out bool canHaveStaircaseAtI))
                {
                    canHaveStaircase = true;
                    return i;
                }
                canHaveStaircase = canHaveStaircase || canHaveStaircaseAtI;
                i++;
                if (pts.Length < i + 5)// At least five points make a staircase
                    return -1;

                segs[segToReplace] = new SegWithIndex(pts, i + 3);
                segToReplace += 1;
                segToReplace %= 4;
            }
        }

        static Point GetFlippedPoint(Point[] pts, int offset) {
            bool horiz = ApproximateComparer.Close(pts[offset].Y, pts[offset + 1].Y);
            return horiz ? new Point(pts[offset + 4].X, pts[offset].Y) : new Point(pts[offset].X, pts[offset + 4].Y);
        }

        /// <summary>
        /// ignoring crossing at a
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="segsToIgnore"></param>
        /// <returns></returns>
        bool Crossing(Point a, Point b, SegWithIndex[] segsToIgnore) => IsCrossing(new LineSegment(a, b), segTree, segsToIgnore);

        /// <summary>
        /// ignoring crossing at ls.Start
        /// </summary>
        /// <param name="ls"></param>
        /// <param name="rTree"></param>
        /// <param name="segsToIgnore"></param>
        /// <returns></returns>
        static bool IsCrossing(LineSegment ls, RTree<SegWithIndex> rTree, SegWithIndex[] segsToIgnore) => rTree.GetAllIntersecting(ls.BoundingBox).Where(seg => !segsToIgnore.Contains(seg)).Any();


        bool IntersectObstacleHierarchy(Point a, Point b, Point c) => IntersectObstacleHierarchy(new LineSegment(a, b)) ||
                   IntersectObstacleHierarchy(new LineSegment(b, c));

        bool IntersectObstacleHierarchy(LineSegment ls) => HierarchyOfObstacles.GetAllIntersecting(ls.BoundingBox).Any(
                    poly => Curve.CurveCurveIntersectionOne(ls, poly, false) != null);

        bool IsStaircase(Point[] pts, int offset, SegWithIndex[] segsToIgnore, out bool canHaveStaircaseAtI) {
            Point a = pts[offset];
            Point b = pts[offset + 1];
            Point c = pts[offset + 2];
            Point d = pts[offset + 3];
            Point f = pts[offset + 4];
            canHaveStaircaseAtI = false;
            if (CompassVector.DirectionsFromPointToPoint(a, b) != CompassVector.DirectionsFromPointToPoint(c, d) ||
                CompassVector.DirectionsFromPointToPoint(b, c) != CompassVector.DirectionsFromPointToPoint(d, f))
                return false;

            c = GetFlippedPoint(pts, offset);
            if (IntersectObstacleHierarchy(b, c, d))
                return false;
            canHaveStaircaseAtI = true;
            return !Crossing(b, c, segsToIgnore);
        }

        Point[] RemoveStaircase(Point[] pts, int staircaseStart) {
            Point a = pts[staircaseStart];
            Point b = pts[staircaseStart + 1];
            bool horiz = Math.Abs(a.Y - b.Y) < ApproximateComparer.DistanceEpsilon/2;
            return RemoveStaircase(pts, staircaseStart, horiz);

        }

        Point[] RemoveStaircase(Point[] pts, int staircaseStart, bool horiz) {
            RemoveSegs(pts);
            Point[] ret = new Point[pts.Length - 2];
            Array.Copy(pts, ret, staircaseStart + 1);
            Point a = pts[staircaseStart + 1];
            Point c = pts[staircaseStart + 3];
            ret[staircaseStart + 1] = horiz ? new Point(c.X, a.Y) : new Point(a.X, c.Y);
            Array.Copy(pts, staircaseStart + 4, ret, staircaseStart + 2, ret.Length - staircaseStart - 2);
            InsertNewSegs(ret, staircaseStart);
            return ret;
        }

        void RemoveSegs(Point[] pts) {
            for (int i = 0; i < pts.Length-1; i++)
                RemoveSeg(new SegWithIndex(pts,i));
        }

        void RemoveSeg(SegWithIndex seg) => segTree.Remove(Rect(seg), seg);


        void InsertNewSegs(Point[] pts, int staircaseStart) {
            InsSeg(pts, staircaseStart);
            InsSeg(pts, staircaseStart+1);
        }

        void InitHierarchies() {
            foreach (Path path in Paths)
                InsertPathSegs(path);
        }

        void InsertPathSegs(Path path) => InsertSegs((Point[])path.PathPoints);


        void InsertSegs(Point[] pts) {
            for (int i = 0; i < pts.Length - 1; i++)
                InsSeg(pts, i);
        }

        void InsSeg(Point[] pts, int i) {
            SegWithIndex seg = new SegWithIndex(pts, i);
            segTree.Add(Rect(seg), seg);
        }

        static Rectangle Rect(SegWithIndex seg) => new Rectangle(seg.Start, seg.End);
    }
}