using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.ProjectionSolver;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    /// <summary>
    /// following paper "Orthogonal Connector Routing" which is included in the project
    /// </summary>
#if TEST_MSAGL
    public
#else
        internal
#endif
    class Nudger {

        bool HasGroups => (null != HierarchyOfGroups) && (HierarchyOfGroups.Count > 0);

        Dictionary<AxisEdge, Polyline> axisEdgesToObstaclesTheyOriginatedFrom;

        List<Path> Paths { get; set; }

        IEnumerable<Polyline> Obstacles { get; set; }
        internal VisibilityGraph PathVisibilityGraph { get; set; }

        /// <summary>
        ///  "nudge" paths to decrease the number of intersections and stores the results inside WidePaths of "paths"
        /// </summary>
        /// <param name="paths">paths through the graph</param>
        /// <param name="cornerFitRad">two parallel paths should be separated by this distance if it is feasible</param>
        /// <param name="obstacles">polygonal convex obstacles organized in a tree; the obstacles here are padded original obstacles</param>
        /// <param name="ancestorsSets"></param>
        /// <returns></returns>
        internal Nudger(IEnumerable<Path> paths, double cornerFitRad, IEnumerable<Polyline> obstacles, 
            Dictionary<Shape, Set<Shape>> ancestorsSets) {
            AncestorsSets = ancestorsSets;
            HierarchyOfGroups = RectangleNode<Shape>.CreateRectangleNodeOnEnumeration(
                    ancestorsSets.Keys.Where(shape => shape.IsGroup).Select(group => new RectangleNode<Shape>(group, group.BoundingBox)));
            Obstacles = obstacles;
            EdgeSeparation = 2 * cornerFitRad;
            Paths = new List<Path>(paths);
            HierarchyOfObstacles =
                RectangleNode<Polyline>.CreateRectangleNodeOnEnumeration(
                    obstacles.Select(p => new RectangleNode<Polyline>(p, p.BoundingBox)));
            MapPathsToTheirObstacles();
        }

        Dictionary<Shape, Set<Shape>> AncestorsSets { get; set; }

        void MapPathsToTheirObstacles() {
            PathToObstacles = [];
            foreach (Path path in Paths)
            {
                MapPathToItsObstacles(path);
            }
        }

        void MapPathToItsObstacles(Path path) {
            RectangleNode<Polyline> startNode = HierarchyOfObstacles.FirstHitNode(path.PathPoints.First(),ObstacleTest);
            RectangleNode<Polyline> endNode = HierarchyOfObstacles.FirstHitNode(path.PathPoints.Last(), ObstacleTest);
            if ((null != startNode) && (null != endNode)) {
                PathToObstacles[path] = new Tuple<Polyline, Polyline>(startNode.UserData, endNode.UserData);
            }
        }

        static HitTestBehavior ObstacleTest(Point pnt, Polyline polyline) => (Curve.PointRelativeToCurveLocation(pnt, polyline) !=
                                                   PointLocation.Outside)
                                                      ? HitTestBehavior.Stop
                                                      : HitTestBehavior.Continue;
        /// <summary>
        /// 
        /// </summary>
        protected RectangleNode<Polyline> HierarchyOfObstacles { get; set; }
        /// <summary>
        /// 
        /// </summary>
        protected RectangleNode<Shape> HierarchyOfGroups { get; set; }

        internal void Calculate(Directions direction, bool mergePaths) {
            NudgingDirection = direction;
            PathRefiner.RefinePaths(Paths, mergePaths);
           // ShowPathsDebug(Paths);
            GetPathOrdersAndPathGraph();
            MapAxisEdgesToTheirObstacles();            
            DrawPaths();
            //ShowPathsDebug(Paths);
        }

        void MapAxisEdgesToTheirObstacles() {
            axisEdgesToObstaclesTheyOriginatedFrom = [];
            foreach (Path path in Paths)
            {
                MapPathEndAxisEdgesToTheirObstacles(path);
            }

            //The assignment above was too greedy. An edge belonging to interiour edges of some path can be marked by mistake.
            foreach (Path path in Paths)
            {
                UmmapPathInteriourFromStrangerObstacles(path);
            }
        }

        void UmmapPathInteriourFromStrangerObstacles(Path path) {
            PathEdge firstUnmappedEdge = FindFirstUnmappedEdge(path);
            if(firstUnmappedEdge==null)
            {
                return;
            }

            PathEdge lastUnmappedEdge = FindLastUnmappedEdge(path);
            for (PathEdge edge = firstUnmappedEdge; edge != null && edge != lastUnmappedEdge; edge = edge.Next)
            {
                axisEdgesToObstaclesTheyOriginatedFrom.Remove(edge.AxisEdge);
            }
        }

        PathEdge FindLastUnmappedEdge(Path path) {
            for (PathEdge edge = path.LastEdge; edge != null; edge = edge.Prev)
            {
                if (edge.AxisEdge.Direction != NudgingDirection)
                {
                    return edge;
                }
            }

            return null;
        }

        PathEdge FindFirstUnmappedEdge(Path path) {
            for (PathEdge edge = path.FirstEdge; edge != null; edge = edge.Next)
            {
                if (edge.AxisEdge.Direction != NudgingDirection)
                {
                    return edge;
                }
            }

            return null;
        }

        void MapPathEndAxisEdgesToTheirObstacles(Path path) {
            if (PathToObstacles.TryGetValue(path, out Tuple<Polyline, Polyline>? coupleOfObstacles))
            {
                ProcessThePathStartToMapAxisEdgesToTheirObstacles(path, coupleOfObstacles.Item1);
                ProcessThePathEndToMapAxisEdgesToTheirObstacles(path, coupleOfObstacles.Item2);
            }
        }

        void ProcessThePathEndToMapAxisEdgesToTheirObstacles(Path path, Polyline endPolyline) {
            for (PathEdge edge = path.LastEdge;
                 edge != null && CompassVector.DirectionsAreParallel(edge.Direction, NudgingDirection);
                 edge = edge.Prev)
            {
                axisEdgesToObstaclesTheyOriginatedFrom[edge.AxisEdge] = endPolyline;
            }
        }

        void ProcessThePathStartToMapAxisEdgesToTheirObstacles(Path path, Polyline startPolyline) {
            for (PathEdge edge = path.FirstEdge;
                 edge != null && CompassVector.DirectionsAreParallel(edge.Direction, NudgingDirection);
                 edge = edge.Next)
            {
                axisEdgesToObstaclesTheyOriginatedFrom[edge.AxisEdge] = startPolyline;
            }
            //possible bug here because an edge might ignore two obstacles if it connects them
        }



        void GetPathOrdersAndPathGraph() {
            CombinatorialNudger combinatorialNudger = new CombinatorialNudger(Paths);
            PathOrders = combinatorialNudger.GetOrder();
            PathVisibilityGraph = combinatorialNudger.PathVisibilityGraph;
        }

        /*
                [Conditional("DEBUG")]
                void CheckPathsForSwitchbacks() {
                    foreach(var p in Paths)
                        CheckForSwitchbacks(p.PathPoints.ToArray());
                }
        */


        Directions NudgingDirection { get; set; }

        #region debugging
#if TEST_MSAGL
        static internal ICurve[] GetCurvesForShow(IEnumerable<Path> paths, IEnumerable<Polyline> obstacles) {
            List<ICurve> ret = new List<ICurve>();
            foreach (Path path in paths) {
                Polyline poly = new Polyline();
                foreach (Point point in path.PathPoints)
                {
                    poly.AddPoint(point);
                }

                ret.Add(poly);
            }
            ret.AddRange(obstacles.Cast<ICurve>());
            return ret.ToArray();
        }
#endif
        #endregion
        void DrawPaths() {
            SetWidthsOfArrowheads();
            CreateLongestNudgedSegments();
            FindFreeSpaceInDirection(PathVisibilityGraph.Edges.Cast<AxisEdge>());
            MoveLongestSegsIdealPositionsInsideFeasibleIntervals();
            PositionShiftedEdges();
        }


        
        
        void SetWidthsOfArrowheads() {
            foreach (Path edgePath in Paths)
            {
                SetWidthsOfArrowheadsForEdge(edgePath);
            }
        }

        static void SetWidthsOfArrowheadsForEdge(Path path) {
            EdgeGeometry edgeGeom = path.EdgeGeometry;
            if (edgeGeom.TargetArrowhead != null) {
                PathEdge pathEdge = path.LastEdge;
                pathEdge.Width = Math.Max(edgeGeom.TargetArrowhead.Width, pathEdge.Width);
            }
            if (edgeGeom.SourceArrowhead != null) {
                PathEdge pathEdge = path.FirstEdge;
                pathEdge.Width = Math.Max(edgeGeom.SourceArrowhead.Width, pathEdge.Width);
            }
        }

        internal double EdgeSeparation { get; set; }

        void PositionShiftedEdges() {
            //we are using 2*cornerFitRadius for the minimal edge separation
            Solver = new UniformOneDimensionalSolver(EdgeSeparation);
            foreach (LongestNudgedSegment segment in LongestNudgedSegs)
            {
                CreateVariablesOfLongestSegment(segment);
            }

            CreateConstraintsOfTheOrder();
            CreateConstraintsBetweenLongestSegments();
            Solver.Solve();
            ShiftPathEdges();
        }

        void MoveLongestSegsIdealPositionsInsideFeasibleIntervals() {
            foreach (LongestNudgedSegment seg in LongestNudgedSegs)
            {
                MoveLongestSegIdealPositionsInsideFeasibleInterval(seg);
            }
        }

        static void MoveLongestSegIdealPositionsInsideFeasibleInterval(LongestNudgedSegment seg) {
            if (seg.IsFixed)
            {
                return;
            }

            double leftBound = seg.GetLeftBound();
            double rightBound = seg.GetRightBound();
            if (seg.IdealPosition < leftBound)
            {
                seg.IdealPosition=leftBound;
            }
            else if( seg.IdealPosition> rightBound)
            {
                seg.IdealPosition=rightBound;
            }
        }

        void ShiftPathEdges() {
            foreach (Path path in Paths)
            {
                path.PathPoints = GetShiftedPoints(path).ToArray();
            }
        }

        IEnumerable<Point> GetShiftedPoints(Path path) => RemoveSwitchbacksAndMiddlePoints(GetShiftedPointsSimple(path));
        /// <summary>
        /// sometimes we have very small mistakes in the positions that have to be fixed
        /// </summary>
        /// <returns></returns>
        static Point Rectilinearise(Point a, Point b) {
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=369
            b = b.Clone();
#endif
            if (a.X != b.X && a.Y != b.Y) {
                double dx = Math.Abs(a.X - b.X);
                double dy = Math.Abs(a.Y - b.Y);
                if (dx < dy)
                {
                    b.X = a.X;
                }
                else
                {
                    b.Y = a.Y;
                }
            }
            return b;
        }

        IEnumerable<Point> GetShiftedPointsSimple(Path path) {
            PathEdge edge = path.FirstEdge;
            yield return ShiftedPoint(edge.Source, edge.LongestNudgedSegment);
            foreach (PathEdge e in path.PathEdges)
            {
                yield return ShiftedEdgePositionOfTarget(e);
            }
        }

        Point ShiftedEdgePositionOfTarget(PathEdge e) => e.LongestNudgedSegment != null || e.Next == null
                       ? ShiftedPoint(e.Target, e.LongestNudgedSegment)
                       : ShiftedPoint(e.Next.Source, e.Next.LongestNudgedSegment);

        Point ShiftedPoint(Point point, LongestNudgedSegment segment) {
            if (segment == null)
            {
                return point;
            }

            double t = Solver.GetVariablePosition(segment.Id);
            return NudgingDirection == Directions.North ? new Point(t, point.Y) : new Point(point.X, -t);
        }

        #region debug
#if TEST_MSAGL
        internal static void ShowPathsFromPoints(IEnumerable<Path> paths, IEnumerable<Polyline> enumerable) {
            List<DebugCurve> dd = new List<DebugCurve>();
            if (enumerable != null)
            {
                dd.AddRange(GetObstacleBoundaries(enumerable, "grey"));
            }

            int i = 0;
            foreach (Path p in paths)
            {
                dd.AddRange(PathDebugCurvesFromPoints(p, DebugCurve.Colors[Math.Min(DebugCurve.Colors.Length, i++)]));
            }

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd);
        }

        static IEnumerable<DebugCurve> PathDebugCurvesFromPoints(Path path, string color) {
            const double startWidth = 0.01;
            const double endWidth = 3;

            Point[] pts = path.PathPoints.ToArray();
            double delta = (endWidth - startWidth) / (pts.Length - 1);
            for (int i = 0; i < pts.Length - 1; i++)
            {
                yield return new DebugCurve(startWidth + delta * i, color, new LineSegment(pts[i], pts[i + 1]));
            }
        }

        internal static void ShowParamPaths(Point s, Point e, params Path[] paths) => ShowOrderedPaths(null, paths, s, e);

        //         ReSharper disable UnusedMember.Local
        internal static void ShowOrderedPaths(IEnumerable<Polyline> obstacles, IEnumerable<Path> paths, Point s, Point e) {
            //           ReSharper restore UnusedMember.Local
            string[] colors = { "red", "green", "blue", "violet", "rose", "black" };

            const double startWidth = 0.001;
            const double endWidth = 0.1;
            List<DebugCurve> dd = new List<DebugCurve>();
            if (obstacles != null)
            {
                dd.AddRange(GetObstacleBoundaries(obstacles, "grey"));
            }

            int i = 0;
            foreach (Path path in paths)
            {
                dd.AddRange(GetTestPathAsDebugCurve(startWidth, endWidth, colors[Math.Min(colors.Length - 1, i++)], path));
            }

            DebugCurve ell = new DebugCurve(1, "black", new Ellipse(0.01, 0.01, s));
            dd.Add(ell);
            dd.Add(new DebugCurve(1, "black", new Ellipse(0.02, 0.02, e)));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd.Concat(GetObstacleBoundaries(obstacles, "lightblue")));
        }

        static IEnumerable<DebugCurve> GetTestPathAsDebugCurve(double startWidth, double endWidth, string color, Path path) {
            if (path.PathEdges.Count() > 0) {
                int count = path.PathEdges.Count();

                double deltaW = count > 1 ? (endWidth - startWidth)/(count - 1) : 1;
                    //if count ==1 the value of deltaW does not matter
                int i = 0;
                foreach (PathEdge e in path.PathEdges)
                {
                    yield return
                        new DebugCurve(150, startWidth + deltaW*(i++), color, new LineSegment(e.Source, e.Target));
                }
            }
            else {
                int count = path.PathPoints.Count();
                Point[] pts = path.PathPoints.ToArray();

                double deltaW = count > 1 ? (endWidth - startWidth) / (count - 1) : 1;
                //if count ==1 the value of deltaW does not matter
                for (int i = 0; i < count - 1;i++ )
                {
                    yield return new DebugCurve(150, startWidth+deltaW*i, color, new LineSegment(pts[i], pts[i+1]));
                }
            }
        }

        internal static IEnumerable<DebugCurve> GetTestEdgePathAsDebugCurves(double startWidth, double endWidth, string color, Path path) {
            int count = path.PathPoints.Count();

            double deltaW = count > 1 ? (endWidth - startWidth) / (count - 1) : 1; //if count ==1 the value of deltaW does not matter
            Point[] points = path.PathPoints.ToArray();
            for (int i = 0; i < points.Length - 1; i++)
            {
                yield return new DebugCurve(125, startWidth + deltaW * i, color, new LineSegment(points[i], points[i + 1]));
            }
        }

        static IEnumerable<DebugCurve> GetEdgePathFromPathEdgesAsDebugCurves(double startWidth, double endWidth, string color, Path path) {
            Point[] points = path.PathPoints.ToArray();

            int count = points.Length;

            double deltaW = count > 1 ? (endWidth - startWidth) / (count - 1) : 1; //if count ==1 the value of deltaW does not matter
            for (int i = 0; i < points.Length - 1; i++)
            {
                yield return new DebugCurve(120, startWidth + deltaW * i, color, new LineSegment(points[i], points[i + 1]));
            }
        }
         // ReSharper disable UnusedMember.Local
        
        static internal void ShowEdgePaths(IEnumerable<Polyline> obstacles, IEnumerable<Path> edgePaths) {
            // ReSharper restore UnusedMember.Local
            List<DebugCurve> debCurves = GetDebCurvesOfPaths(obstacles, edgePaths);

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debCurves);
        }

        internal static List<DebugCurve> GetDebCurvesOfPaths(IEnumerable<Polyline> enumerable, IEnumerable<Path> edgePaths) {
            List<DebugCurve> debCurves = GetObstacleBoundaries(enumerable, "black");
            int i = 0;

            foreach (Path edgePath in edgePaths)
            {
                debCurves.AddRange(GetTestEdgePathAsDebugCurves(0.2, 4, DebugCurve.Colors[(i++) % DebugCurve.Colors.Length], edgePath));
            }

            return debCurves;
        }

        internal static void ShowPathsInLoop(IEnumerable<Polyline> enumerable, IEnumerable<Path> edgePaths, Point point) {
            foreach (Path? edgePath in edgePaths.Where(path=>(path.PathPoints.First()-point).Length<1 ||(path.PathPoints.Last()-point).Length<1 )) {
                List<DebugCurve> debCurves = GetObstacleBoundaries(enumerable, "black");
                debCurves.AddRange(GetTestEdgePathAsDebugCurves(0.1, 4, "red", edgePath));
                LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debCurves);
            }
        }


        // ReSharper disable UnusedMember.Local
        void ShowLongSegsWithIdealPositions(Directions dir) {
            // ReSharper restore UnusedMember.Local
            List<DebugCurve> debCurves = GetObstacleBoundaries(Obstacles, "black");
            int i = 0;

            debCurves.AddRange(LongestNudgedSegs.Select(ls => DebugCurveOfLongSeg(ls, DebugCurve.Colors[i++ % DebugCurve.Colors.Length], dir)));

            DebugCurveCollection.WriteToFile(debCurves, "c:/tmp/longSegs");

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debCurves);
        }

        static DebugCurve DebugCurveOfLongSeg(LongestNudgedSegment ls, string s, Directions dir) => new DebugCurve(1, s, LineSegOfLongestSeg(ls, dir));

        static ICurve LineSegOfLongestSeg(LongestNudgedSegment ls, Directions dir) {
            PointProjection projectionToDir = dir == Directions.East ? (PointProjection)(p => p.X) : (p => p.Y);
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            foreach (PathEdge edge in ls.Edges) {
                UpdateMinMaxWithPoint(ref min, ref max, projectionToDir, edge.Source);
                UpdateMinMaxWithPoint(ref min, ref max, projectionToDir, edge.Target);
            }
            return dir == Directions.East
                       ? new LineSegment(min, -ls.IdealPosition, max, -ls.IdealPosition)
                       : new LineSegment(ls.IdealPosition, min, ls.IdealPosition, max);
        }

        static void UpdateMinMaxWithPoint(ref double min, ref double max, PointProjection projectionToDir, Point point) {
            double p = projectionToDir(point);
            if (min > p)
            {
                min = p;
            }

            if (max < p)
            {
                max = p;
            }
        }


        // ReSharper disable UnusedMember.Local
        void ShowPathsDebug(IEnumerable<Path> edgePaths) {
            List<DebugCurve> debCurves = GetObstacleBoundaries(Obstacles, "black");
            int i = 0;

            foreach (Path edgePath in edgePaths)
            {
                debCurves.AddRange(GetEdgePathFromPathEdgesAsDebugCurves(0.01, 0.4, DebugCurve.Colors[(i++) % DebugCurve.Colors.Length], edgePath));
            }

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debCurves);
        }

        static IEnumerable<DebugCurve> PathDebugCurves(Path path, string color) {
            IEnumerable<DebugCurve> d = path.PathEdges.Select(e => new DebugCurve(70, 0.5, color, new LineSegment(e.Source, e.Target)));
            return d.Concat(MarkPathVerts(path));
        }

        private static IEnumerable<DebugCurve> MarkPathVerts(Path path) {
            bool first = true;
            Point p = new Point();
            foreach (Point p0 in path.PathPoints) {
                if (first) {
                    yield return new DebugCurve(200, 1, "violet", CurveFactory.CreateDiamond(5, 5, p0));
                    first = false;
                } else
                {
                    yield return new DebugCurve(100, 0.5, "brown", CurveFactory.CreateEllipse(1.5, 1.5, p0));
                }

                p = p0;
            }
            yield return new DebugCurve(200, 1, "green", CurveFactory.CreateDiamond(3, 3, p));
        }

        static internal IEnumerable<DebugCurve> PathDebugCurvesFromPoint(Path path) {
            List<Point> l = new List<Point>(path.PathPoints);
            for (int i = 0; i < l.Count - 1; i++)
            {
                yield return new DebugCurve(4, "red", new LineSegment(l[i], l[i + 1]));
            }
        }
        //
        // ReSharper disable UnusedMember.Local
        //        void ShowEdgesOfEdgePath(Path path){
        // ReSharper restore UnusedMember.Local
        //            string[] colors = {"red", "brown", "purple"};
        //            const double w0 = 1;
        //            const double w1 = 3;
        //            double dw = (w1 - w0)/path.OrientedSubpaths.Count;
        //            int i = 0;
        //            var dc = new List<DebugCurve>();
        //            foreach (var s in path.OrientedSubpaths){
        //                dc.AddRange(SubpathDebugCurves(w0 + dw*i, colors[Math.Min(i++, colors.Length - 1)], s));
        //            }
        //            LayoutAlgorithmSettings.ShowDebugCurves(dc.ToArray());
        //        }
        //
        //        static IEnumerable<DebugCurve> SubpathDebugCurves(double w, string color, OrientedSubpath subpath){
        //            return subpath.LinkedPath.Select(e => new DebugCurve(w, color, new LineSegment(e.Source.Point, e.Target.Point)));
        //        }


        static internal List<DebugCurve> GetObstacleBoundaries(IEnumerable<Polyline> obstacles, string color) {
            List<DebugCurve> debugCurves = new List<DebugCurve>();
            if (obstacles != null)
            {
                debugCurves.AddRange(obstacles.Select(poly => new DebugCurve(50, 0.3, color, poly)));
            }

            return debugCurves;
        }
#endif
        #endregion

        void CreateConstraintsBetweenLongestSegments() {
            foreach (LongestNudgedSegment segment in LongestNudgedSegs)
            {
                CreateConstraintsBetweenLongestSegmentsForSegment(segment);
            }
        }

        void CreateConstraintsBetweenLongestSegmentsForSegment(LongestNudgedSegment segment) {
            Set<LongestNudgedSegment> rightNeighbors = new Set<LongestNudgedSegment>();
            foreach (PathEdge pathEdge in segment.Edges) {
                AxisEdge axisEdge = pathEdge.AxisEdge;
                if (axisEdge != null)
                {
                    foreach (AxisEdge rightNeiAxisEdge in axisEdge.RightNeighbors)
                    {
                        foreach (LongestNudgedSegment longSeg in rightNeiAxisEdge.LongestNudgedSegments)
                        {
                            rightNeighbors.Insert(longSeg);
                        }
                    }
                }
            }

            foreach (LongestNudgedSegment seg in rightNeighbors)
            {
                ConstraintTwoLongestSegs(segment, seg);
            }
        }

        void CreateConstraintsOfTheOrder() {
            foreach (KeyValuePair<AxisEdge, List<PathEdge>> kv in PathOrders)
            {
                if (ParallelToDirection(kv.Key, NudgingDirection))
                {
                    CreateConstraintsOfThePathOrder(kv.Value);
                }
            }
        }

        static bool ParallelToDirection(VisibilityEdge edge, Directions direction) {
            switch (direction) {
                case Directions.North:
                case Directions.South:
                    return ApproximateComparer.Close(edge.SourcePoint.X, edge.TargetPoint.X);
                default:
                    return ApproximateComparer.Close(edge.SourcePoint.Y, edge.TargetPoint.Y);
            }
        }

        void CreateConstraintsOfThePathOrder(IEnumerable<PathEdge> pathOrder) {
            PathEdge prevEdge = null;

            foreach (PathEdge? pathEdge in pathOrder.Where(p => p.LongestNudgedSegment != null)) {
                if (prevEdge != null)
                {
                    ConstraintTwoLongestSegs(prevEdge.LongestNudgedSegment, pathEdge.LongestNudgedSegment);
                }

                prevEdge = pathEdge;
            }
        }

        void ConstraintTwoLongestSegs(LongestNudgedSegment prevSeg, LongestNudgedSegment seg) {
            if (!prevSeg.IsFixed || !seg.IsFixed)
            {
                Solver.AddConstraint(prevSeg.Id, seg.Id);
            }
        }

        UniformOneDimensionalSolver Solver { get; set; }

        void CreateVariablesOfLongestSegment(LongestNudgedSegment segment) {
            if (!segment.IsFixed) {
                double leftBound = segment.GetLeftBound();
                double rightBound = segment.GetRightBound();
                if (leftBound >= rightBound) {//don't move the segment from the way it was generated
                    Solver.AddFixedVariable(segment.Id, SegmentPosition(segment, NudgingDirection));
                    segment.IsFixed = true;
                } else {
                    Solver.AddVariable(segment.Id, SegmentPosition(segment, NudgingDirection), segment.IdealPosition, segment.Width);
         //           Debug.Assert(leftBound + Curve.DistanceEpsilon < rightBound); //this assert does not hold for overlaps
                    if (leftBound != double.NegativeInfinity)
                    {
                        Solver.SetLowBound(leftBound, segment.Id);
                    }

                    if (rightBound != double.PositiveInfinity)
                    {
                        Solver.SetUpperBound(segment.Id, rightBound);
                    }
                }
            } else
            {
                Solver.AddFixedVariable(segment.Id, SegmentPosition(segment, NudgingDirection));
            }
        }

        static double SegmentPosition(SegmentBase segment, Directions direction) => direction == Directions.North ? segment.Start.X : -segment.Start.Y;

#if TEST_MSAGL
        // ReSharper disable UnusedMember.Local
        void ShowSegmentBounds(LongestNudgedSegment segment) {
            // ReSharper restore UnusedMember.Local
            List<DebugCurve> dd = GetObstacleBoundaries(Obstacles, "black");
            double segtop = segment.Edges.Max(e => Math.Max(e.Source.Y, e.Target.Y));
            double segbottom = segment.Edges.Min(e => Math.Min(e.Source.Y, e.Target.Y));
            double segx = segment.Start.X;
            DebugCurve seg = new DebugCurve(80, 1, "brown", new LineSegment(new Point(segx, segbottom), new Point(segx, segtop)));

            DebugCurve leftbound = new DebugCurve(80, 1.0, "red", new LineSegment(new Point(segment.GetLeftBound(), segbottom),
                                                                           new Point(segment.GetLeftBound(), segtop)));
            DebugCurve rightbound = new DebugCurve(80, 1, "green", new LineSegment(new Point(segment.GetRightBound(), segbottom),
                            new Point(segment.GetRightBound(), segtop)));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd.Concat(new[] { seg, leftbound, rightbound }));
        }

        // ReSharper disable UnusedMember.Local
        void ShowSegment(LongestNudgedSegment segment) {
            // ReSharper restore UnusedMember.Local
            List<DebugCurve> dd = GetObstacleBoundaries(Obstacles, "black");
            double segtop = segment.Edges.Max(e => Math.Max(e.Source.Y, e.Target.Y));
            double segbottom = segment.Edges.Min(e => Math.Min(e.Source.Y, e.Target.Y));
            double segx = segment.Start.X;
            DebugCurve seg = new DebugCurve(80, 1, "brown", new LineSegment(new Point(segx, segbottom), new Point(segx, segtop)));

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd.Concat(new[] { seg }));

        }
#endif

        List<LongestNudgedSegment> LongestNudgedSegs { get; set; }

        Dictionary<AxisEdge, List<PathEdge>> PathOrders { get; set; }
        /// <summary>
        /// maps each path to the pair of obstacles; the first element of the pair is 
        /// where the path starts and the second where the path ends
        /// </summary>
        Dictionary<Path, Tuple<Polyline, Polyline>> PathToObstacles { get; set; }


        void FindFreeSpaceInDirection(IEnumerable<AxisEdge> axisEdges) {
            BoundAxisEdgesByRectsKnownInAdvance();
            FreeSpaceFinder freeSpaceFinder = new FreeSpaceFinder(NudgingDirection, Obstacles,
                                                      axisEdgesToObstaclesTheyOriginatedFrom, PathOrders, axisEdges);
            freeSpaceFinder.FindFreeSpace();
        }

        void BoundAxisEdgesByRectsKnownInAdvance() {
            foreach (Path path in Paths) {
                if (HasGroups)
                {
                    BoundPathByMinCommonAncestors(path);
                }

                BoundAxisEdgesAdjacentToSourceAndTargetOnEdge(path);               
            }
        }

        void BoundPathByMinCommonAncestors(Path path) {
            foreach (Rectangle rect in GetMinCommonAncestors(path.EdgeGeometry).Select(sh => sh.BoundingBox))
            {
                foreach (
                    AxisEdge? edge in
                        path.PathEdges.Select(e => e.AxisEdge).Where(
                            axisEdge => axisEdge.Direction == NudgingDirection)
                    )
                {
                    BoundAxisEdgeByRect(rect, edge);
                }
            }
        }

        
        IEnumerable<Shape> GetMinCommonAncestors(EdgeGeometry edgeGeometry) {
            if (PortToShapes == null)
            {
                PortToShapes = MapPortsToShapes(AncestorsSets.Keys);
            }

            Set<Shape> commonAncestors = AncestorsForPort(edgeGeometry.SourcePort)*
                                  AncestorsForPort(edgeGeometry.TargetPort);
            return commonAncestors.Where(anc => !anc.Children.Any(child=>commonAncestors.Contains(child)));
        }
        /// <summary>
        /// 
        /// </summary>
        protected Dictionary<Port, Shape> PortToShapes { get; private set; }

        Set<Shape> AncestorsForPort(Port port) {
            if (PortToShapes.TryGetValue(port, out Shape? shape))
            {
                return AncestorsSets[shape];
            }

            // This is a FreePort or Waypoint; return all spatial parents.
            return new Set<Shape>(HierarchyOfGroups.AllHitItems(new Rectangle(port.Location, port.Location), null));
        }


        void BoundAxisEdgeAdjacentToObstaclePort(Port port, AxisEdge axisEdge) {
            if (port is WaypointPort || (port.Curve == null && port.PortEntry == null))
            {
                BoundAxisByPoint(port.Location, axisEdge);
            }
            else if (port.PortEntry == null) {
                if (port.Curve.BoundingBox.Contains(port.Location))
                {
                    BoundAxisEdgeByRect(port.Curve.BoundingBox, axisEdge);
                }
            } else {
                if (port.PortEntry is PortEntryOnCurve portEntry)
                {
                    if (FindPortEntryRectCrossingAxisEdge(portEntry, axisEdge, out Rectangle rect))
                    {
                        BoundAxisEdgeByRect(rect, axisEdge);
                    }
                }
            }
        }

        void BoundAxisByPoint(Point point, AxisEdge axisEdge) {
            if (axisEdge != null && axisEdge.Direction == NudgingDirection)
            {
                if (NudgingDirection == Directions.North) {
                    axisEdge.BoundFromLeft(point.X);
                    axisEdge.BoundFromRight(point.X);
                } else {
                    axisEdge.BoundFromLeft(-point.Y);
                    axisEdge.BoundFromRight(-point.Y);
                }
            }
        }

        static bool FindPortEntryRectCrossingAxisEdge(PortEntryOnCurve portEntry, AxisEdge axisEdge, out Rectangle rect) {
            Rectangle ar = new Rectangle(axisEdge.SourcePoint, axisEdge.TargetPoint);           
            foreach (Rectangle r in portEntry.AllowedRectangles) {
                if(r.Intersects(ar)) {
                    rect = r;
                    return true;
                }                    
            }
            rect = Rectangle.CreateAnEmptyBox();
            return false;
        }

        void BoundAxisEdgesAdjacentToSourceAndTargetOnEdge(Path path) {
            BoundAxisEdgeAdjacentToObstaclePort(path.EdgeGeometry.SourcePort, path.FirstEdge.AxisEdge);
            BoundAxisEdgeAdjacentToObstaclePort(path.EdgeGeometry.TargetPort, path.LastEdge.AxisEdge);
        }

        void BoundAxisEdgeByRect(Rectangle rectangle, AxisEdge axisEdge) {
            if (axisEdge != null && axisEdge.Direction == NudgingDirection)
            {
                if (NudgingDirection == Directions.North) {
                    axisEdge.BoundFromLeft(rectangle.Left);
                    axisEdge.BoundFromRight(rectangle.Right);
                } else {
                    axisEdge.BoundFromLeft(-rectangle.Top);
                    axisEdge.BoundFromRight(-rectangle.Bottom);
                }
            }
        }

     
        void CreateLongestNudgedSegments() {
            PointProjection projectionToPerp =
                NudgingDirection == Directions.East ? (PointProjection)FreeSpaceFinder.MinusY : FreeSpaceFinder.X;

            LongestNudgedSegs = [];
            foreach (Path path in Paths)
            {
                CreateLongestNudgedSegmentsForPath(path, projectionToPerp);
            }
        }


        void CreateLongestNudgedSegmentsForPath(Path path, PointProjection projectionToPerp) {
            //ShowEdgesOfEdgePath(path);
            GoOverPathAndCreateLongSegs(path);
            CalculateIdealPositionsForLongestSegs(path, projectionToPerp);
        }

        static void CalculateIdealPositionsForLongestSegs(Path path, PointProjection projectionToPerp) {
            LongestNudgedSegment currentLongSeg = null;
            LongestNudgedSegment ret = null;
            double prevOffset = projectionToPerp(path.Start);
            foreach (PathEdge edge in path.PathEdges) {
                if (edge.LongestNudgedSegment != null) {
                    currentLongSeg = edge.LongestNudgedSegment;
                    if (ret != null) {
                        double t;
                        SetIdealPositionForSeg(ret, t = projectionToPerp(ret.Start), prevOffset,
                                               projectionToPerp(currentLongSeg.Start));
                        prevOffset = t;
                        ret = null;
                    }
                } else if (currentLongSeg != null) {
                    ret = currentLongSeg;
                    currentLongSeg = null;
                }
            }
            if (ret != null)
            {
                SetIdealPositionForSeg(ret, projectionToPerp(ret.Start), prevOffset, projectionToPerp(path.End));
            }
            else if (currentLongSeg != null)
            {
                currentLongSeg.IdealPosition = projectionToPerp(currentLongSeg.Start);
            }
        }

        static void SetIdealPositionForSeg(LongestNudgedSegment segment, double segPosition, double offset0, double offset1) {
            double max = Math.Max(offset0, offset1);
            double min = Math.Min(offset0, offset1);
            if (min + ApproximateComparer.DistanceEpsilon < segPosition)
            {
                if (segPosition < max)
                {
                    segment.IdealPosition = 0.5 * (max + min);
                }
                else
                {
                    segment.IdealPosition = max;
                }
            }
            else
            {
                segment.IdealPosition = min;
            }
        }

        void GoOverPathAndCreateLongSegs(Path path) {
            LongestNudgedSegment currentLongestSeg = null;

            Directions oppositeDir = CompassVector.OppositeDir(NudgingDirection);

            foreach (PathEdge edge in path.PathEdges) {
                Directions edgeDir = edge.Direction;
                if (edgeDir == NudgingDirection || edgeDir == oppositeDir) {
                    if (currentLongestSeg == null)
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=368
                    {
                        edge.LongestNudgedSegment = currentLongestSeg = new LongestNudgedSegment(LongestNudgedSegs.Count);
                        LongestNudgedSegs.Add(edge.LongestNudgedSegment);
                    }
#else
                        LongestNudgedSegs.Add(
                            edge.LongestNudgedSegment =
                            currentLongestSeg = new LongestNudgedSegment(LongestNudgedSegs.Count));
#endif
                    else
                    {
                        edge.LongestNudgedSegment = currentLongestSeg;
                    }

                    if (edge.IsFixed)
                    {
                        currentLongestSeg.IsFixed = true;
                    }
                } else {
                    //the edge is perpendicular to "direction"
                    edge.LongestNudgedSegment = null;
                    currentLongestSeg = null;
                }
            }
        }

        static IEnumerable<Point> BuildPolylineForPath(Path path) {
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=369
            var points = path.PathPoints.Select(p=>p.Clone()).ToArray();
#else
            Point[] points = path.PathPoints.ToArray();
#endif
            ExtendPolylineToPorts(ref points, path);
            for(int i=0;i<points.Length-1;i++)
            {
                Debug.Assert(CompassVector.IsPureDirection(points[i], points[i+1]));
            }

            return points;
        }

        static void ExtendPolylineToPorts(ref Point[] points, Path path) {
            ExtendPolylineToSourcePort(ref points, path.EdgeGeometry.SourcePort.Location);
            ExtendPolylineToTargetPort(ref points, path.EdgeGeometry.TargetPort.Location);
            
            // In some overlapped cases where the source or target vertex used for the path
            // coincides with the target or source port location, we can end up with a single-point
            // path.  In that case, we just force a straightline path.
            if (points.Length < 2) {
                points = new Point[2];
                points[0] = path.EdgeGeometry.SourcePort.Location;
                points[1] = path.EdgeGeometry.TargetPort.Location;
            }
        }

        static void ExtendPolylineToTargetPort(ref Point[] points, Point location) {
            int n = points.Length - 1;
            Directions dir = CompassVector.VectorDirection(points[n - 1], points[n]);
            if (ProjectionsAreClose(points[n-1], dir, location)) {
                //it might be that the last point on polyline is at the port already
                //then we just drop the last point
                points = points.Take(n).ToArray();
                return;
            }
            if (dir == Directions.East || dir == Directions.West)
            {
                points[n].X = location.X;
            }
            else
            {
                points[n].Y = location.Y;
            }
        }

        static bool ProjectionsAreClose(Point a, Directions dir, Point b) {
            if (dir == Directions.East || dir == Directions.West)
            {
                return ApproximateComparer.Close(a.X, b.X);
            }

            return ApproximateComparer.Close(a.Y, b.Y);
        }

        static void ExtendPolylineToSourcePort(ref Point[] points, Point location) {
            Directions dir = CompassVector.VectorDirection(points[0], points[1]);
            if (ProjectionsAreClose(points[1], dir, location)) {
                //it might be that the second point on polyline is at the port already
                //then we just drop the first point
                points = points.Skip(1).ToArray();
                return;
            }
            if (dir == Directions.East || dir == Directions.West)
            {
                points[0].X = location.X;
            }
            else
            {
                points[0].Y = location.Y;
            }
        }

        static IEnumerable<Point> RemoveSwitchbacksAndMiddlePoints(IEnumerable<Point> points) {
            IEnumerator<Point> en = points.GetEnumerator();
            en.MoveNext();
            Point a = en.Current;
            yield return a;
            en.MoveNext();
            Point b = en.Current;
            Directions prevDir = (b - a).CompassDirection;

            while (en.MoveNext()) {
                Directions dir = (en.Current - b).CompassDirection;
                if (dir == prevDir || CompassVector.OppositeDir(dir) == prevDir || dir == Directions.None) //we continue walking along the same straight line, maybe going backwards!
                {
                    b = en.Current;
                }
                else {
                    if (!ApproximateComparer.Close(a, b)) {//make sure that we are not returning the same point twice                        
                        yield return a=Rectilinearise(a, b);
                    }
                    b = en.Current;
                    prevDir = dir;
                }
            }
            if (!ApproximateComparer.Close(a, b))
            {
                yield return Rectilinearise(a,b);
            }
        }


        /// <summary>
        /// this function defines the final path coordinates
        /// </summary>
        /// <param name="paths">the set of paths, point sequences</param>
        /// <param name="cornerFitRadius">the radius of the arc inscribed into the path corners</param>
        /// <param name="paddedObstacles">an enumeration of padded obstacles</param>
        /// <param name="ancestorsSets"></param>
        /// <param name="removeStaircases"></param>
        /// <returns>the mapping of the path to its modified path</returns>
        internal static void NudgePaths(IEnumerable<Path> paths, double cornerFitRadius, IEnumerable<Polyline> paddedObstacles, Dictionary<Shape, Set<Shape>> ancestorsSets, bool removeStaircases) {
            if (!paths.Any())
            {
                return;
            }

            Nudger nudger = new Nudger(paths, cornerFitRadius, paddedObstacles, ancestorsSets);
            nudger.Calculate(Directions.North, true);
            nudger.Calculate(Directions.East, false);
            nudger.Calculate(Directions.North, false);
            if (removeStaircases)
            {
                nudger.RemoveStaircases();
            }

            foreach (Path path in paths)
            {
                path.EdgeGeometry.Curve = new Polyline(BuildPolylineForPath(path));
            }
        }

        void RemoveStaircases() => StaircaseRemover.RemoveStaircases(Paths, HierarchyOfObstacles);
        /*
        void RemoveStaircasesFromPath(Path path) {

            var points = (Point[]) path.PathPoints;
            if (points.Length <= 4) return;
            int i = FindStaircase(points);
            if (i == -1) return;
            var linkedList = new LinkedList<Point>(points);
            var node = linkedList.First;
            for(int j=0;j<i;j++)
                node = node.Next; //getting to the i-th node

            RemoveStaircaseOnNode(node);
            node = node.Next;
            while(true) {
                node = FindStaircaseAfterNode(node);
                if (node == null) break;
                RemoveStaircaseOnNode(node);
                node = node.Next;
            }
        }

        LinkedListNode<Point> FindStaircaseAfterNode(LinkedListNode<Point> n) {
            do {
                var nn = n.Next;
                var nnn = nn.Next;
                var nnnn = nnn.Next;
                if (nnn.Next == null)
                    return null;
                if (IsStaircase(n.Value, nn.Value, nnn.Value, nnnn.Value))
                    return n;
                n = nn;
            } while (true);
        }

        bool IsStaircase(Point a, Point b, Point c, Point d) {

            return false;
        }

        void RemoveStaircaseOnNode(LinkedListNode<Point> node) {
            throw new NotImplementedException();
        }

        int FindStaircase(Point[] points) {
            throw new NotImplementedException();
        }
         */

        internal static Dictionary<Port, Shape> MapPortsToShapes(IEnumerable<Shape> listOfShapes) {
            Dictionary<Port, Shape> portToShapes = new Dictionary<Port, Shape>();
            foreach (Shape shape in listOfShapes)
            {
                foreach (Port port in shape.Ports)
                {
                    portToShapes[port] = shape;
                }
            }

            return portToShapes;
        }
    }
}
