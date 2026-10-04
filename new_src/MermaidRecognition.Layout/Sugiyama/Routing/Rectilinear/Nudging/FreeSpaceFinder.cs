using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    /// <summary>
    /// The class is looking for the free space around AxisEdges
    /// </summary>
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=301
    internal class FreeSpaceFinder : LineSweeperBase {
#else
    internal class FreeSpaceFinder : LineSweeperBase, IComparer<AxisEdgesContainer> {
#endif
        static double AreaComparisonEpsilon = ApproximateComparer.IntersectionEpsilon;
        readonly PointProjection xProjection;

        internal static double X(Point p) => p.X;
        internal static double MinusY(Point p) => -p.Y;

        readonly RbTree<AxisEdgesContainer> edgeContainersTree;
        internal Dictionary<AxisEdge, List<PathEdge>> PathOrders { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="obstacles"></param>
        /// <param name="axisEdgesToObstaclesTheyOriginatedFrom"></param>
        /// <param name="pathOrders"></param>
        /// <param name="axisEdges">edges to find the empty space around</param>
        internal FreeSpaceFinder(Directions direction, IEnumerable<Polyline> obstacles, Dictionary<AxisEdge,Polyline> axisEdgesToObstaclesTheyOriginatedFrom, Dictionary<AxisEdge, List<PathEdge>> pathOrders, 
            IEnumerable<AxisEdge> axisEdges): base(obstacles, new CompassVector(direction).ToPoint()) {
            DirectionPerp = new CompassVector(direction).Right.ToPoint();
            PathOrders = pathOrders;
            xProjection = direction == Directions.North ? (PointProjection)X : MinusY;
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=301
            edgeContainersTree = new RbTree<AxisEdgesContainer>(new FreeSpaceFinderComparer(this));
#else
            edgeContainersTree = new RbTree<AxisEdgesContainer>(this);
#endif
            SweepPole = CompassVector.VectorDirection(SweepDirection);
            Debug.Assert(CompassVector.IsPureDirection(SweepPole));
            AxisEdges = axisEdges;
            AxisEdgesToObstaclesTheyOriginatedFrom = axisEdgesToObstaclesTheyOriginatedFrom;
            
        }

        Dictionary<AxisEdge, Polyline> AxisEdgesToObstaclesTheyOriginatedFrom { get; set; }

        protected Directions SweepPole { get; set; }

     //   List<Path> EdgePaths { get; set; }

        //VisibilityGraph PathVisibilityGraph { get; set; }

        /// <summary>
        /// calculates the right offsets
        /// </summary>
        internal void FindFreeSpace() {
            InitTheQueueOfEvents();
            ProcessEvents();
        //    ShowAxisEdges();            
        }

        
        void ProcessEvents() {
            while (EventQueue.Count > 0)
                ProcessEvent(EventQueue.Dequeue());
        }

        void ProcessEvent(SweepEvent sweepEvent) {
            //            if (SweepDirection.Y == 1 && (sweepEvent.Site - new Point(75.45611, 15.21524)).Length < 0.1)
            // ShowAtPoint(sweepEvent.Site);

            if (sweepEvent is VertexEvent vertexEvent)
                ProcessVertexEvent(vertexEvent);
            else
            {
                Z = GetZ(sweepEvent.Site);
                if (sweepEvent is AxisEdgeLowPointEvent lowEdgeEvent)
                    ProcessLowEdgeEvent(lowEdgeEvent);
                else
                    ProcessHighEdgeEvent((AxisEdgeHighPointEvent)sweepEvent);
            }
        }

        void ProcessHighEdgeEvent(AxisEdgeHighPointEvent edgeForNudgingHighPointEvent) {
            AxisEdge edge = edgeForNudgingHighPointEvent.AxisEdge;
            RemoveEdge(edge);
            ConstraintEdgeWithObstaclesAtZ(edge, edge.Target.Point);
        }

        void ProcessLowEdgeEvent(AxisEdgeLowPointEvent lowEdgeEvent) {

            AxisEdge edge = lowEdgeEvent.AxisEdge;

            RBNode<AxisEdgesContainer> containerNode = GetOrCreateAxisEdgesContainer(edge);
            containerNode.Item.AddEdge(edge);
            RBNode<AxisEdgesContainer> prev = edgeContainersTree.Previous(containerNode);
            if (prev != null)
                foreach (AxisEdge prevEdge in prev.Item)
                    foreach (AxisEdge ed in containerNode.Item)
                        TryToAddRightNeighbor(prevEdge, ed);

            RBNode<AxisEdgesContainer> next = edgeContainersTree.Next(containerNode);
            if (next != null)
                foreach (AxisEdge ed in containerNode.Item)
                    foreach (AxisEdge neEdge in next.Item)
                        TryToAddRightNeighbor(ed, neEdge);
            ConstraintEdgeWithObstaclesAtZ(edge, edge.Source.Point);
        }

        void TryToAddRightNeighbor(AxisEdge leftEdge, AxisEdge rightEdge) {
            if (ProjectionsOfEdgesOverlap(leftEdge, rightEdge))
                leftEdge.AddRightNeighbor(rightEdge);
        }

        bool ProjectionsOfEdgesOverlap(AxisEdge leftEdge, AxisEdge rightEdge) => SweepPole == Directions.North
                       ? !(leftEdge.TargetPoint.Y < rightEdge.SourcePoint.Y - ApproximateComparer.DistanceEpsilon ||
                           rightEdge.TargetPoint.Y < leftEdge.SourcePoint.Y - ApproximateComparer.DistanceEpsilon)
                       : !(leftEdge.TargetPoint.X < rightEdge.SourcePoint.X - ApproximateComparer.DistanceEpsilon ||
                           rightEdge.TargetPoint.X < leftEdge.SourcePoint.X - ApproximateComparer.DistanceEpsilon);

#if TEST_MSAGL
        // ReSharper disable UnusedMember.Local
        void DebShowEdge(AxisEdge edge, Point point) =>
                // ReSharper restore UnusedMember.Local
                // if (InterestingEdge(edge))
                ShowEdge(edge, point);


        // ReSharper disable SuggestBaseTypeForParameter
        void ShowEdge(AxisEdge edge, Point point){
            // ReSharper restore SuggestBaseTypeForParameter

            IEnumerable<DebugCurve> dd = GetObstacleBoundaries("black");
            DebugCurve seg = new DebugCurve( 1, "red", new LineSegment(edge.Source.Point, edge.Target.Point));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd.Concat(
                new[]{seg ,new DebugCurve("blue",CurveFactory.CreateEllipse(3, 3, point))}));
  

        }

        IEnumerable<DebugCurve> GetObstacleBoundaries(string color) => Obstacles.Select(p => new DebugCurve(1, color, p));
#endif


        /// <summary>
        /// 
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="point">a point on the edge on Z level</param>
        void ConstraintEdgeWithObstaclesAtZ(AxisEdge edge, Point point) {
            Debug.Assert(point==edge.Source.Point || point == edge.Target.Point);
            ConstraintEdgeWithObstaclesAtZFromLeft(edge, point);
            ConstraintEdgeWithObstaclesAtZFromRight(edge, point);
        }

        void ConstraintEdgeWithObstaclesAtZFromRight(AxisEdge edge, Point point) {
            RBNode<SegmentBase> node = GetActiveSideFromRight(point);
            if (node == null) return;
            if (NotRestricting(edge, ((LeftObstacleSide) node.Item).Polyline)) return;
            Point x = ObstacleSideComparer.IntersectionOfSideAndSweepLine(node.Item);
            edge.BoundFromRight(x*DirectionPerp);
        }

        RBNode<SegmentBase> GetActiveSideFromRight(Point point) => LeftObstacleSideTree.FindFirst(side =>
                                           PointToTheLeftOfLineOrOnLineLocal(point, side.Start, side.End));

        void ConstraintEdgeWithObstaclesAtZFromLeft(AxisEdge edge, Point point){
            //    ShowNudgedSegAndPoint(point, nudgedSegment);
            RBNode<SegmentBase> node = GetActiveSideFromLeft(point);
            if (node == null) return;
            if (NotRestricting(edge, ((RightObstacleSide) node.Item).Polyline)) return;
            Point x = ObstacleSideComparer.IntersectionOfSideAndSweepLine(node.Item);
            edge.BoundFromLeft(x * DirectionPerp);
        }

        static bool PointToTheLeftOfLineOrOnLineLocal(Point a, Point linePoint0, Point linePoint1) => Point.SignedDoubledTriangleArea(a, linePoint0, linePoint1) > -AreaComparisonEpsilon;

        static bool PointToTheRightOfLineOrOnLineLocal(Point a, Point linePoint0, Point linePoint1) => Point.SignedDoubledTriangleArea(linePoint0, linePoint1, a) < AreaComparisonEpsilon;

        RBNode<SegmentBase> GetActiveSideFromLeft(Point point) => RightObstacleSideTree.FindLast(side =>
                                                  PointToTheRightOfLineOrOnLineLocal(point, side.Start, side.End));
        #region debug
#if TEST_MSAGL
        // ReSharper disable UnusedMember.Local
        void ShowPointAndEdge(Point point, AxisEdge edge) {
// ReSharper restore UnusedMember.Local
            List<ICurve> curves = GetCurves(point, edge);

            LayoutAlgorithmSettings.Show(curves.ToArray());
        }

        // ReSharper disable UnusedMember.Local
        void ShowPointAndEdgeWithSweepline(Point point, AxisEdge edge) {
// ReSharper restore UnusedMember.Local
            List<ICurve> curves = GetCurves(point, edge);

            curves.Add(new LineSegment(SweepDirection * Z + 10 * DirectionPerp, SweepDirection * Z - 10 * DirectionPerp));

            LayoutAlgorithmSettings.Show(curves.ToArray());
        }

        List<ICurve> GetCurves(Point point, AxisEdge edge) {
            ICurve ellipse = CurveFactory.CreateEllipse(3, 3, point);
            List<ICurve> curves = new List<ICurve>(Obstacles.Select(o => o as ICurve)){ellipse,
                                                                            new LineSegment(edge.Source.Point, edge.Target.Point
                                                                                )};

            if (edge.RightBound < double.PositiveInfinity) {
                double rightOffset = edge.RightBound;
                Point del = DirectionPerp * rightOffset;
                curves.Add(new LineSegment(edge.Source.Point + del, edge.Target.Point + del));
            }
            if (edge.LeftBound > double.NegativeInfinity) {
                double leftOffset = edge.LeftBound;
                Point del = DirectionPerp * leftOffset;
                curves.Add(new LineSegment(edge.Source.Point + del, edge.Target.Point  + del));
            }

            curves.AddRange((from e in PathOrders.Keys
                             let a = e.SourcePoint
                             let b = e.TargetPoint
                             select new CubicBezierSegment(a, a*0.8 + b*0.2, a*0.2 + b*0.8, b)).Cast<ICurve>());

            return curves;
        }

        List<DebugCurve> GetCurvesTest(Point point){
            ICurve ellipse = CurveFactory.CreateEllipse(3, 3, point);
            List<DebugCurve> curves = new List<DebugCurve>(Obstacles.Select(o => new DebugCurve(100, 1, "black", o)))
                         {new DebugCurve(100, 1, "red", ellipse)};
            curves.AddRange(from e in edgeContainersTree
                             from axisEdge in e
                             let a = axisEdge.Source.Point
                             let b = axisEdge.Target.Point
                             select new DebugCurve(100, 1, "green", new LineSegment(a, b)));


            curves.AddRange(RightNeighborsCurvesTest(edgeContainersTree));

            return curves;
        }

        static IEnumerable<DebugCurve> RightNeighborsCurvesTest(IEnumerable<AxisEdgesContainer> rbTree) {
            foreach (AxisEdgesContainer container in rbTree) {
                foreach (AxisEdge edge  in container) {
                    foreach (AxisEdge rn in edge.RightNeighbors) {
                        yield return new DebugCurve(100,1,"brown",new LineSegment(EdgeMidPoint(edge), EdgeMidPoint(rn)));
                    }
                }
            }
        }

        static Point EdgeMidPoint(AxisEdge edge) => 0.5 * (edge.SourcePoint + edge.TargetPoint);

        // ReSharper disable UnusedMember.Local
        void ShowAxisEdges() {
            // ReSharper restore UnusedMember.Local
            List<DebugCurve> dd = new List<DebugCurve>(GetObstacleBoundaries("black"));
            int i = 0;
            foreach (AxisEdge axisEdge in AxisEdges) {
                string color = DebugCurve.Colors[i];
                dd.Add(new DebugCurve(200, 1, color,
                                       new LineSegment(axisEdge.Source.Point, axisEdge.Target.Point)));
                Point perp = axisEdge.Direction == Directions.East ? new Point(0, 1) : new Point(-1, 0);
                if (axisEdge.LeftBound != double.NegativeInfinity) {
                    dd.Add(new DebugCurve(200, 0.5, color,
                        new LineSegment(axisEdge.Source.Point + axisEdge.LeftBound * perp, axisEdge.Target.Point + axisEdge.LeftBound * perp)));
                }
                if (axisEdge.RightBound != double.PositiveInfinity) {
                    dd.Add(new DebugCurve(200, 0.5, color,
                        new LineSegment(axisEdge.Source.Point - axisEdge.RightBound * perp, axisEdge.Target.Point - axisEdge.RightBound * perp)));
                }
                i = (i + 1) % DebugCurve.Colors.Length;
            }
            DebugCurveCollection.WriteToFile(dd, "c:/tmp/ae");
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd);
        }

        // ReSharper disable UnusedMember.Local
        void ShowAtPoint(Point point) {
            // ReSharper restore UnusedMember.Local
            List<DebugCurve> curves = GetCurvesTest(point);
            LayoutAlgorithmSettings.ShowDebugCurves(curves.ToArray());
        }
#endif
        #endregion
        RBNode<AxisEdgesContainer> GetOrCreateAxisEdgesContainer(AxisEdge edge) {
            Point source = edge.Source.Point;

            RBNode<AxisEdgesContainer> ret = GetAxisEdgesContainerNode(source);
      
            if(ret!=null)
                return ret;

            return edgeContainersTree.Insert(new AxisEdgesContainer(source));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="point">the point has to be on the same line as the container</param>
        /// <returns></returns>
        RBNode<AxisEdgesContainer> GetAxisEdgesContainerNode(Point point){
            double prj = xProjection( point);
            RBNode<AxisEdgesContainer> ret =
                edgeContainersTree.FindFirst(cont => xProjection(cont.Source) >= prj-ApproximateComparer.DistanceEpsilon/2);
            if(ret != null)
                if (xProjection(ret.Item.Source) <= prj+ApproximateComparer.DistanceEpsilon/2) 
                    return ret;
            return null;
        }



        void ProcessVertexEvent(VertexEvent vertexEvent) {
            Z = GetZ(vertexEvent);
            if (vertexEvent is LeftVertexEvent leftVertexEvent)
                ProcessLeftVertex(leftVertexEvent, vertexEvent.Vertex.NextOnPolyline);
            else
            {
                if (vertexEvent is RightVertexEvent rightVertexEvent)
                    ProcessRightVertex(rightVertexEvent, vertexEvent.Vertex.PrevOnPolyline);
                else
                {
                    ProcessLeftVertex(vertexEvent, vertexEvent.Vertex.NextOnPolyline);
                    ProcessRightVertex(vertexEvent, vertexEvent.Vertex.PrevOnPolyline);
                }
            }
        }

        void ProcessRightVertex(VertexEvent rightVertexEvent, PolylinePoint nextVertex){
            Debug.Assert(Z == rightVertexEvent.Site*SweepDirection);

            Point site = rightVertexEvent.Site;
            ProcessPrevSegmentForRightVertex(rightVertexEvent, site);

            Point delta = nextVertex.Point - rightVertexEvent.Site;
            double deltaX = delta*DirectionPerp;
            double deltaZ = delta*SweepDirection;
            if (deltaZ <= ApproximateComparer.DistanceEpsilon){
                if (deltaX > 0 && deltaZ >= 0)
                    EnqueueEvent(new RightVertexEvent(nextVertex));
                else
                    RestrictEdgeContainerToTheRightOfEvent(rightVertexEvent.Vertex);
            }
            else{
                //deltaZ>epsilon
                InsertRightSide(new RightObstacleSide(rightVertexEvent.Vertex));
                EnqueueEvent(new RightVertexEvent(nextVertex));
                RestrictEdgeContainerToTheRightOfEvent(rightVertexEvent.Vertex);

            }
        }

        private void RestrictEdgeContainerToTheRightOfEvent(PolylinePoint polylinePoint) {
            Point site = polylinePoint.Point;
            double siteX = xProjection(site);
            RBNode<AxisEdgesContainer> containerNode =
                edgeContainersTree.FindFirst
                    (container => siteX <= xProjection(container.Source));

            if (containerNode != null)
                foreach (AxisEdge edge in containerNode.Item)
                    if (!NotRestricting(edge, polylinePoint.Polyline))
                        edge.BoundFromLeft(DirectionPerp*site);
        }

        bool NotRestricting(AxisEdge edge, Polyline polyline) => AxisEdgesToObstaclesTheyOriginatedFrom.TryGetValue(edge, out Polyline? p) && p == polyline;


        void ProcessPrevSegmentForRightVertex(VertexEvent rightVertexEvent, Point site) {
            Point prevSite = rightVertexEvent.Vertex.NextOnPolyline.Point;
            Point delta = site - prevSite;
            double deltaZ = delta * SweepDirection;
            if (deltaZ > ApproximateComparer.DistanceEpsilon)
                RemoveRightSide(new RightObstacleSide(rightVertexEvent.Vertex.NextOnPolyline));
        }


        void RemoveEdge(AxisEdge edge){
            RBNode<AxisEdgesContainer> containerNode = GetAxisEdgesContainerNode(edge.Source.Point);
            containerNode.Item.RemoveAxis(edge);
            if(containerNode.Item.IsEmpty())
                edgeContainersTree.DeleteNodeInternal(containerNode);
            
         //   Console.WriteLine(edgeContainersTree);
        }


        void ProcessLeftVertex(VertexEvent leftVertexEvent, PolylinePoint nextVertex){
            Debug.Assert(Z == leftVertexEvent.Site*SweepDirection);

            Point site = leftVertexEvent.Site;
            ProcessPrevSegmentForLeftVertex(leftVertexEvent, site);

            Point delta = nextVertex.Point - leftVertexEvent.Site;
            double deltaX = delta*DirectionPerp;
            double deltaZ = delta*SweepDirection;
            if (deltaZ <= ApproximateComparer.DistanceEpsilon ){
                if (deltaX < 0 && deltaZ >= 0)
                    EnqueueEvent(new LeftVertexEvent(nextVertex));
            }
            else{
                //deltaZ>epsilon
                InsertLeftSide(new LeftObstacleSide(leftVertexEvent.Vertex));
                EnqueueEvent(new LeftVertexEvent(nextVertex));
            }
            //ShowAtPoint(leftVertexEvent.Site);
            RestrictEdgeFromTheLeftOfEvent(leftVertexEvent.Vertex);
        }

        private void RestrictEdgeFromTheLeftOfEvent(PolylinePoint polylinePoint) {
            //ShowAtPoint(site);
            Point site = polylinePoint.Point;
            RBNode<AxisEdgesContainer> containerNode = GetContainerNodeToTheLeftOfEvent(site);

            if (containerNode != null)
                foreach (AxisEdge edge in containerNode.Item)
                    if (!NotRestricting(edge, polylinePoint.Polyline))
                        edge.BoundFromRight(site*DirectionPerp);
        }

        RBNode<AxisEdgesContainer> GetContainerNodeToTheLeftOfEvent(Point site) {
            double siteX = xProjection(site);
            return
                edgeContainersTree.FindLast(
                    container => xProjection(container.Source)<= siteX);
            //                Point.PointToTheRightOfLineOrOnLine(site, container.Source,
            //                                                                                                container.UpPoint));
        }


        private void ProcessPrevSegmentForLeftVertex(VertexEvent leftVertexEvent, Point site) {
            Point prevSite = leftVertexEvent.Vertex.PrevOnPolyline.Point;
            Point delta = site - prevSite;
            double deltaZ = delta * SweepDirection;
            if (deltaZ > ApproximateComparer.DistanceEpsilon)
                RemoveLeftSide(new LeftObstacleSide(leftVertexEvent.Vertex.PrevOnPolyline));
        }


        void InitTheQueueOfEvents() {
            InitQueueOfEvents();
            foreach (AxisEdge axisEdge in AxisEdges)
                EnqueueEventsForEdge(axisEdge);
        }

        protected IEnumerable<AxisEdge> AxisEdges { get; set; }

        void EnqueueEventsForEdge(AxisEdge edge) {
            if (EdgeIsParallelToSweepDir(edge)) {
                EnqueueEvent(EdgeLowPointEvent(edge, edge.Source.Point));
                EnqueueEvent(EdgeHighPointEvent(edge, edge.Target.Point));
            }
        }

        bool EdgeIsParallelToSweepDir(AxisEdge edge) => edge.Direction == SweepPole || edge.Direction == CompassVector.OppositeDir(SweepPole);


        static SweepEvent EdgeHighPointEvent(AxisEdge edge, Point point) => new AxisEdgeHighPointEvent(edge, point);

        static SweepEvent EdgeLowPointEvent(AxisEdge edge, Point point) => new AxisEdgeLowPointEvent(edge, point);

#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=301
        public class FreeSpaceFinderComparer : IComparer<AxisEdgesContainer>
        {
            private FreeSpaceFinder m_Owner;
            public FreeSpaceFinderComparer(FreeSpaceFinder owner)
            {
                m_Owner = owner;
            }
            public int Compare(AxisEdgesContainer? x, AxisEdgesContainer? y) {
            if (x is null) return y is null ? 0 : -1; if (y is null) return 1;
                ValidateArg.IsNotNull(x, "x");
                ValidateArg.IsNotNull(y, "y");
                return (x.Source * m_Owner.DirectionPerp).CompareTo(y.Source * m_Owner.DirectionPerp);
            }
        }
#else
        public int Compare(AxisEdgesContainer? x, AxisEdgesContainer? y) {
            if (x is null) return y is null ? 0 : -1; if (y is null) return 1;
            ValidateArg.IsNotNull(x, "x");
            ValidateArg.IsNotNull(y, "y");
            return (x.Source * DirectionPerp).CompareTo(y.Source * DirectionPerp);
        }
#endif
    }
}
