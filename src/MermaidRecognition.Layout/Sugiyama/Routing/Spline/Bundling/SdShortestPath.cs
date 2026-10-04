using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    internal class SdShortestPath {
        internal VisibilityGraph VisibilityGraph { get; set; }
        internal Func<EdgeGeometry, List<Shape>> MakeTransparentShapesOfEdgeGeometry { get; set; }
        internal BundlingSettings BundlingSettings { get; set; }
        internal EdgeGeometry[] EdgeGeometries { get; set; }
        internal RectangleNode<Polyline> ObstacleHierarchy { get; set; }

        SdVertex[] vertexArray;
        internal Cdt Cdt { get; set; }
        Set<CdtEdge> Gates { get; set; }

        readonly Dictionary<EdgeGeometry, List<SdBoneEdge>> EdgesToRoutes = [];
        readonly Dictionary<EdgeGeometry, SdVertex> EdgesToRouteSources = [];

        EdgeGeometry CurrentEdgeGeometry;
        Dictionary<VisibilityVertex, SdVertex> VisibilityVerticesToSdVerts;
        double LengthCoefficient;
        GenericBinaryHeapPriorityQueue<SdVertex> Queue;
        double LowestCostToTarget;
        SdVertex ClosestTargetVertex;
        double capacityOverlowPenaltyMultiplier;
        Polyline sourceLoosePoly;
        Polyline targetLoosePoly;

        internal SdShortestPath(Func<EdgeGeometry, List<Shape>> makeTransparentShapesOfEdgeGeometryAndGetTheShapes, Cdt cdt, Set<CdtEdge> gates) {
            MakeTransparentShapesOfEdgeGeometry = makeTransparentShapesOfEdgeGeometryAndGetTheShapes;
            Cdt = cdt;
            Gates = gates;
        }

        void CreateGraphElements() {
            foreach (SdVertex sdVertex in vertexArray) {
                VisibilityVertex vv = sdVertex.VisibilityVertex;
                foreach (VisibilityEdge vEdge in vv.InEdges) {
                    SdBoneEdge boneEdge = new SdBoneEdge(vEdge, VisibilityVerticesToSdVerts[vEdge.Source], VisibilityVerticesToSdVerts[vEdge.Target]);
                    SdVertex otherSdVertex = VisibilityVerticesToSdVerts[vEdge.Source];
                    sdVertex.InBoneEdges.Add(boneEdge);
                    otherSdVertex.OutBoneEdges.Add(boneEdge);
                }
            }
        }

        void CreateRoutingGraph() {
            vertexArray = new SdVertex[VisibilityGraph.VertexCount];
            int i = 0;
            VisibilityVerticesToSdVerts = [];
            foreach (VisibilityVertex v in VisibilityGraph.Vertices()) {
                SdVertex sdVert = new SdVertex(v);
                vertexArray[i++] = sdVert;
                VisibilityVerticesToSdVerts[v] = sdVert;
            }

            CreateGraphElements();
        }

        /// <summary>
        /// routing of the edges minimizing (ink+path length+capacity penalty)
        /// </summary>
        internal void RouteEdges() {
            Initialize();
            RestoreCapacities();
            foreach (EdgeGeometry edgeGeometry in EdgeGeometries) {
                EdgesToRoutes[edgeGeometry] = RouteEdge(edgeGeometry);
            }

            RerouteEdges();

            foreach (EdgeGeometry edgeGeometry in EdgeGeometries)
            {
                SetEdgeGeometryCurve(edgeGeometry);
            }
        }

        void SetEdgeGeometryCurve(EdgeGeometry edgeGeometry) {
            Polyline poly = new Polyline();
            SdVertex curV = EdgesToRouteSources[edgeGeometry];
            poly.AddPoint(curV.Point);
            foreach (SdBoneEdge edge in EdgesToRoutes[edgeGeometry]) {
                if (edge.SourcePoint == curV.Point) {
                    poly.AddPoint(edge.TargetPoint);
                    curV = edge.Target;
                }
                else {
                    poly.AddPoint(edge.SourcePoint);
                    curV = edge.Source;
                }
            }

            edgeGeometry.Curve = poly;
            if (edgeGeometry.SourcePort is ClusterBoundaryPort clusterSourcePort)
            {
                ExtendPolylineStartToClusterBoundary(poly, clusterSourcePort.Curve);
            }

            if (edgeGeometry.TargetPort is ClusterBoundaryPort clusterTargetPort)
            {
                ExtendPolylineEndToClusterBoundary(poly, clusterTargetPort.Curve);
            }
        }

        static void ExtendPolylineEndToClusterBoundary(Polyline poly, ICurve curve) {
            double par = curve.ClosestParameter(poly.End);
            poly.AddPoint(curve[par]);
        }

        static void ExtendPolylineStartToClusterBoundary(Polyline poly, ICurve curve) {
            double par = curve.ClosestParameter(poly.Start);
            poly.PrependPoint(curve[par]);
        }

        void RerouteEdges() {
            RestoreCapacities();
            foreach (EdgeGeometry edgeGeometry in EdgeGeometries) {
                List<SdBoneEdge> newRoute = RerouteEdge(edgeGeometry);
                EdgesToRoutes[edgeGeometry] = newRoute;
            }
        }

        void RestoreCapacities() {
            if (Cdt != null)
            {
                Cdt.RestoreEdgeCapacities();
            }
        }

        /// <summary>
        /// Reroute edge
        /// </summary>
        List<SdBoneEdge> RerouteEdge(EdgeGeometry edgeGeometry) {
            List<SdBoneEdge> route = EdgesToRoutes[edgeGeometry];

            foreach (SdBoneEdge edge in route)
            {
                edge.RemoveOccupiedEdge();
            }

            return RouteEdge(edgeGeometry);
        }

        List<SdBoneEdge> RouteEdge(EdgeGeometry edgeGeometry) {
            CurrentEdgeGeometry = edgeGeometry;
            for (int i = 0; i < vertexArray.Length; i++) {
                SdVertex sdv = vertexArray[i];
                sdv.SetPreviousToNull();
                sdv.IsSourceOfRouting = sdv.IsTargetOfRouting = false;
            }

            List<Shape> transparentShapes = MakeTransparentShapesOfEdgeGeometry(edgeGeometry);
            List<SdBoneEdge> ret = RouteEdgeWithGroups();

            foreach (Shape shape in transparentShapes)
            {
                shape.IsTransparent = false;
            }

            /*List<LineSegment> ls = new List<LineSegment>();
            foreach (var e in ret)
                ls.Add(new LineSegment(e.SourcePoint, e.TargetPoint));
            SplineRouter.ShowVisGraph(this.VisibilityGraph, ObstacleHierarchy.GetAllLeaves(), null, ls);*/

            return ret;
        }

        List<SdBoneEdge> RouteEdgeWithGroups() {
            for (int i = 0; i < 2; i++) {
                SetLengthCoefficient();
                Queue = new GenericBinaryHeapPriorityQueue<SdVertex>();
                SetPortVerticesAndObstacles(CurrentEdgeGeometry.SourcePort, true, out sourceLoosePoly);
                SetPortVerticesAndObstacles(CurrentEdgeGeometry.TargetPort, false, out targetLoosePoly);
                List<SdBoneEdge> ret = RouteOnKnownSourceTargetVertices((CurrentEdgeGeometry.TargetPort.Location - CurrentEdgeGeometry.SourcePort.Location).Normalize(), i == 0);
                if (ret != null)
                {
                    return ret;
                }

                for (int j = 0; j < vertexArray.Length; j++) {
                    vertexArray[j].SetPreviousToNull();
                }
            }
            //SplineRouter.ShowVisGraph(this.VisibilityGraph, ObstacleHierarchy.GetAllLeaves(), null, new[] { new LineSegment(
           // CurrentEdgeGeometry.SourcePort.Location, CurrentEdgeGeometry.TargetPort.Location)});
            throw new InvalidOperationException(); //cannot find a path
        }


        List<SdBoneEdge> RouteOnKnownSourceTargetVertices(Point pathDirection, bool lookingForMonotonePath) {
            LowestCostToTarget = double.PositiveInfinity;
            ClosestTargetVertex = null;
            while (Queue.Count > 0) {
                SdVertex bestNode = Queue.Dequeue(out double hu);
                if (hu >= LowestCostToTarget)
                {
                    continue;
                }
                //update the rest
                for (int i = 0; i < bestNode.OutBoneEdges.Count; i++) {
                    SdBoneEdge outBoneEdge = bestNode.OutBoneEdges[i];
                    if (outBoneEdge.IsPassable)
                    {
                        ProcessOutcomingBoneEdge(bestNode, outBoneEdge, pathDirection, lookingForMonotonePath);
                    }
                }

                for (int i = 0; i < bestNode.InBoneEdges.Count; i++) {
                    SdBoneEdge inBoneEdge = bestNode.InBoneEdges[i];
                    if (inBoneEdge.IsPassable)
                    {
                        ProcessIncomingBoneEdge(bestNode, inBoneEdge, pathDirection, lookingForMonotonePath);
                    }
                }
            }

            return GetPathAndUpdateRelatedCosts();
        }

        void ProcessOutcomingBoneEdge(SdVertex v, SdBoneEdge outBoneEdge, Point pathDirection, bool lookingForMonotonePath) {
            Debug.Assert(v == outBoneEdge.Source);
            if (lookingForMonotonePath && pathDirection * (outBoneEdge.TargetPoint - outBoneEdge.SourcePoint) < 0)
            {
                return;
            }

            ProcessBoneEdge(v, outBoneEdge.Target, outBoneEdge);
        }

        void ProcessIncomingBoneEdge(SdVertex v, SdBoneEdge inBoneEdge, Point pathDirection, bool lookingForMonotonePath) {
            Debug.Assert(v == inBoneEdge.Target);
            if (lookingForMonotonePath && pathDirection * (inBoneEdge.SourcePoint - inBoneEdge.TargetPoint) < 0)
            {
                return;
            }

            ProcessBoneEdge(v, inBoneEdge.Source, inBoneEdge);
        }

        void ProcessBoneEdge(SdVertex v, SdVertex queueCandidate, SdBoneEdge boneEdge) {
            double newCost = GetEdgeAdditionalCost(boneEdge, v.Cost);
            if (queueCandidate.Cost <= newCost)
            {
                return;
            }

            queueCandidate.Cost = newCost;
            queueCandidate.PrevEdge = boneEdge;
            if (Queue.ContainsElement(queueCandidate))
            {
                Queue.DecreasePriority(queueCandidate, newCost);
            }
            else {
                if (queueCandidate.IsTargetOfRouting) {
                    double costToTarget = 0;
                    if (CurrentEdgeGeometry.TargetPort is ClusterBoundaryPort)
                    {
                        costToTarget = LengthCoefficient * (queueCandidate.Point - CurrentEdgeGeometry.TargetPort.Location).Length;
                    }

                    if (newCost + costToTarget < LowestCostToTarget) {
                        LowestCostToTarget = newCost + costToTarget;
                        ClosestTargetVertex = queueCandidate;
                    }
                    return; //do not enqueue the target vertices
                }
                Enqueue(queueCandidate);
            }
        }

#if DEBUG && TEST_MSAGL
        //        void DebugShow(SdSimpleVertex prevElement, SdBoneEdge outBoneEdge) {
        //            SplineRouter.ShowVisGraph(this.VisibilityGraph,
        //                                      this.ObstacleHierarchy.GetAllLeaves(),
        //                                      prevElement.BoneEdge != null ?
        //                                       new[] { new LineSegment(prevElement.BoneEdge.SourcePoint, prevElement.BoneEdge.TargetPoint) } : null,
        //                                      new[] {(ICurve) new LineSegment(outBoneEdge.SourcePoint, outBoneEdge.TargetPoint) ,
        //                                      new Ellipse(5,5,outBoneEdge.TargetPoint)});
        //        }
        //
        //        void DebugShowIn(SdSimpleVertex prevElement, SdBoneEdge inBoneEdge) {
        //            SplineRouter.ShowVisGraph(this.VisibilityGraph,
        //                                      this.ObstacleHierarchy.GetAllLeaves(),
        //                                      prevElement.BoneEdge != null ?
        //                                                                    new[] { new LineSegment(prevElement.BoneEdge.SourcePoint, prevElement.BoneEdge.TargetPoint) } : null,
        //                                      new[] {(ICurve) new LineSegment(inBoneEdge.SourcePoint, inBoneEdge.TargetPoint) ,
        //                                      new Ellipse(2,2,inBoneEdge.TargetPoint)});
        //        }
#endif

        List<SdBoneEdge> GetPathAndUpdateRelatedCosts() {
            //restore the path by moving backwards
            SdVertex current = ClosestTargetVertex;
            if (current == null)
            {
                return null;
            }

            List<SdBoneEdge> result = new List<SdBoneEdge>();

            while (current.PrevEdge != null) {
                result.Add(current.PrevEdge);
                RegisterPathInBoneEdge(current.PrevEdge);
                current = current.Prev;
            }

            EdgesToRouteSources[CurrentEdgeGeometry] = current;

            result.Reverse();

            Debug.Assert(result.Count > 0);
            return result;
        }

        void RegisterPathInBoneEdge(SdBoneEdge boneEdge) {
            boneEdge.AddOccupiedEdge();
            if (Cdt != null && BundlingSettings.CapacityOverflowCoefficient != 0)
            {
                UpdateResidualCostsOfCrossedCdtEdges(boneEdge);
            }
        }

        void UpdateResidualCostsOfCrossedCdtEdges(SdBoneEdge boneEdge) {
            foreach (CdtEdge cdtEdge in boneEdge.CrossedCdtEdges) {
                if (AdjacentToSourceOrTarget(cdtEdge))
                {
                    continue;
                }

                if (cdtEdge.ResidualCapacity == cdtEdge.Capacity)
                {
                    cdtEdge.ResidualCapacity -= CurrentEdgeGeometry.LineWidth;
                }
                else
                {
                    cdtEdge.ResidualCapacity -= (CurrentEdgeGeometry.LineWidth + BundlingSettings.EdgeSeparation);
                }
                //TODO: can we have negative here?
                //Debug.Assert(cdtEdge.ResidualCapacity >= 0);
            }
        }

        double H(SdVertex v) => v.Cost + LengthCoefficient * (v.Point - CurrentEdgeGeometry.TargetPort.Location).Length;

        double GetEdgeAdditionalCost(SdBoneEdge boneEdge, double previousCost) {
            double len = (boneEdge.TargetPoint - boneEdge.SourcePoint).Length;
            return LengthCoefficient * len + previousCost +
                (boneEdge.IsOccupied ? 0 : BundlingSettings.InkImportance * len) + CapacityOverflowCost(boneEdge);
        }

        double CapacityOverflowCost(SdBoneEdge boneEdge) {
            if (Cdt == null || BundlingSettings.CapacityOverflowCoefficient == 0)
            {
                return 0;
            }

            double ret = 0;
            foreach (CdtEdge cdtEdge in CrossedCdtEdgesOfBoneEdge(boneEdge)) {
                ret += CostOfCrossingCdtEdgeLocal(capacityOverlowPenaltyMultiplier, BundlingSettings, CurrentEdgeGeometry, cdtEdge);
            }
            return ret;
        }

        IEnumerable<CdtEdge> CrossedCdtEdgesOfBoneEdge(SdBoneEdge boneEdge) {
            if (boneEdge.CrossedCdtEdges != null)
            {
                return boneEdge.CrossedCdtEdges;
            }
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=368
            boneEdge.CrossedCdtEdges = ThreadBoneEdgeThroughCdt(boneEdge);
            return boneEdge.CrossedCdtEdges;
#else
            return boneEdge.CrossedCdtEdges = ThreadBoneEdgeThroughCdt(boneEdge);
#endif
        }

        Set<CdtEdge> ThreadBoneEdgeThroughCdt(SdBoneEdge boneEdge) {
            Point start = boneEdge.SourcePoint;
            CdtTriangle currentTriangle = boneEdge.Source.Triangle;
            Debug.Assert(Cdt.PointIsInsideOfTriangle(start, currentTriangle));
            Set<CdtEdge> crossedEdges = new Set<CdtEdge>();
            Point end = boneEdge.TargetPoint;
            if (Cdt.PointIsInsideOfTriangle(end, currentTriangle))
            {
                return crossedEdges;
            }

            CdtThreader threader = new CdtThreader(currentTriangle, start, end);
            while (threader.MoveNext()) {
                CdtEdge piercedEdge = threader.CurrentPiercedEdge;
                Debug.Assert(piercedEdge != null);
                if (Gates.Contains(piercedEdge))
                {
                    crossedEdges.Insert(piercedEdge);
                }
            }

            /*
            CdtEdge piercedEdge = CdtIntersections.FindFirstPiercedEdge(currentTriangle, start, end, out negativeSign, out positiveSign, this.Cdt );
            Debug.Assert(piercedEdge != null);
      
            do {
                if (Gates.Contains(piercedEdge))
                    crossedEdges.Insert(piercedEdge);
            }
            while (CdtIntersections.FindNextPierced(start, end, ref currentTriangle, ref piercedEdge, ref negativeSign, ref positiveSign));
            */
            //if(ddd(boneEdge))
            //CdtSweeper.ShowFront(Cdt.GetTriangles(),null,new []{new LineSegment(boneEdge.SourcePoint,boneEdge.TargetPoint)}, crossedEdges.Select(e=>new LineSegment(e.upperSite.Point,e.lowerSite.Point)));

            return crossedEdges;
        }

        //TODO: method incorrect since id doesn't check AdjacentToSourceOrTarget condition
        internal static double CostOfCrossingCdtEdge(double capacityOverflMult, BundlingSettings bundlingSettings, EdgeGeometry currentEdgeGeometry, CdtEdge e) {
            double w = currentEdgeGeometry.LineWidth;
            if (e.Capacity != e.ResidualCapacity)
            {
                w += bundlingSettings.EdgeSeparation;
            }

            double del = e.ResidualCapacity - w;
            if (del >= 0)
            {
                return 0;
            }

            return -del * capacityOverflMult;
        }

        double CostOfCrossingCdtEdgeLocal(double capacityOverflMult, BundlingSettings bundlingSettings, EdgeGeometry currentEdgeGeometry, CdtEdge e) {
            if (AdjacentToSourceOrTarget(e))
            {
                return 0;
            }

            return CostOfCrossingCdtEdge(capacityOverflMult, bundlingSettings, currentEdgeGeometry, e);
        }

        bool AdjacentToSourceOrTarget(CdtEdge e) => e.upperSite.Owner == sourceLoosePoly || e.lowerSite.Owner == sourceLoosePoly || e.upperSite.Owner == targetLoosePoly || e.lowerSite.Owner == targetLoosePoly;

        double GetIdealDistanceBetweenHookUpAnywherePortAndPort(HookUpAnywhereFromInsidePort hookPort, Port port) {
            Polyline poly = hookPort.LoosePolyline;
            Point point = port.Location;
            double closestPar = poly.ClosestParameter(point);
            return (point - poly[closestPar]).Length;
        }

        double GetIdealDistanceBetweenClusterPortAndPort(ClusterBoundaryPort clsp, Port port) {
            Polyline poly = clsp.LoosePolyline;
            if (port is ClusterBoundaryPort otherClsp)
            {
                Polygon polygon0 = new Polygon(poly);
                Polygon polygon1 = new Polygon(otherClsp.LoosePolyline);
                return Polygon.Distance(polygon0, polygon1);
            }

            Point point = port.Location;
            double closestPar = poly.ClosestParameter(point);
            return (point - poly[closestPar]).Length;
        }

        void SetLengthCoefficient() {
            double idealEdgeLength = GetIdealDistanceBetweenSourceAndTarget(CurrentEdgeGeometry);
            LengthCoefficient = BundlingSettings.PathLengthImportance / idealEdgeLength;
        }

        double GetIdealDistanceBetweenSourceAndTarget(EdgeGeometry edgeGeometry) => (edgeGeometry.SourcePort.Location - edgeGeometry.TargetPort.Location).Length;

        void SetPortVerticesAndObstacles(Port port, bool sources, out Polyline poly) {
            poly = null;
            if (port is ClusterBoundaryPort cbport)
            {
                //SplineRouter.ShowVisGraph(this.VisibilityGraph, this.ObstacleHierarchy.GetAllLeaves(), null, new[]{cbport.LoosePolyline});
                poly = cbport.LoosePolyline;
                foreach (Point point in poly)
                {
                    double initialCost = 0;
                    if (sources)
                    {
                        //we prefer paths starting from the center of the group
                        initialCost = LengthCoefficient * (point - CurrentEdgeGeometry.SourcePort.Location).Length;
                    }
                    AddAndEnqueueVertexToEnds(point, sources, initialCost);
                }
            }
            else
            {
                if (port is HookUpAnywhereFromInsidePort anywherePort)
                {
                    poly = anywherePort.LoosePolyline;
                    foreach (Point point in poly)
                    {
                        AddAndEnqueueVertexToEnds(point, sources, 0);
                    }
                }
                else
                {
                    AddAndEnqueueVertexToEnds(port.Location, sources, 0);
                    Polyline[] polys = ObstacleHierarchy.GetNodeItemsIntersectingRectangle(port.Curve.BoundingBox).ToArray();
                    double mindiag = polys[0].BoundingBox.Diagonal;
                    poly = polys[0];
                    for (int i = 1; i < polys.Length; i++)
                    {
                        Polyline pl = polys[i];
                        double diag = pl.BoundingBox.Diagonal;
                        if (diag < mindiag)
                        {
                            mindiag = diag;
                            poly = pl;
                        }
                    }

                }
            }
        }

        void Enqueue(SdVertex simpleSdVertex) => Queue.Enqueue(simpleSdVertex, H(simpleSdVertex));

        void AddAndEnqueueVertexToEnds(Point point, bool isSource, double initialCost) {
            VisibilityVertex v = FindVertex(point);
            SdVertex sdVert = VisibilityVerticesToSdVerts[v];
            if (isSource) {
                sdVert.IsSourceOfRouting = true;
                sdVert.Cost = initialCost;
                Enqueue(sdVert);
            }
            else {
                sdVert.IsTargetOfRouting = true;
            }
        }

        VisibilityVertex FindVertex(Point p) => VisibilityGraph.FindVertex(p) ?? VisibilityGraph.FindVertex(ApproximateComparer.Round(p));/*  if (r == null) {
                  SplineRouter.ShowVisGraph(this.VisibilityGraph, this.ObstacleHierarchy.GetAllLeaves(), null,
                  new[] { new Ellipse(5, 5, p) });               
              }
              return r;*/

        void Initialize() {
            CreateRoutingGraph();
            if (Cdt != null) {
                capacityOverlowPenaltyMultiplier = CapacityOverflowPenaltyMultiplier(BundlingSettings);
                SetVertexTriangles();
                CalculateCapacitiesOfTrianglulation();
            }
        }

        void CalculateCapacitiesOfTrianglulation() {
            foreach (CdtEdge e in Gates)
            {
                CalculateCdtEdgeCapacityForEdge(e);
            }
        }

        static void CalculateCdtEdgeCapacityForEdge(CdtEdge e) {
            if (e.Constrained || e.CwTriangle == null || e.CcwTriangle == null)
            {
                return; //this is a convex hull edge or an obstacle edge
            }

            Polyline? startPoly = e.upperSite.Owner as Polyline;
            Polyline? endPoly = e.lowerSite.Owner as Polyline;
            if (startPoly != endPoly) {
                //e.Capacity = Polygon.Distance(new Polygon(startPoly), new Polygon(endPoly)); //todo: cache this
                //e.Capacity = (e.upperSite.Point - e.lowerSite.Point).Length;
                double distA = Polygon.Distance(new Polygon(startPoly), e.lowerSite.Point);
                double distB = Polygon.Distance(new Polygon(endPoly), e.upperSite.Point);
                e.Capacity = (distA + distB) / 2;
            }
            //else - it is a diagonal of an obstacle, do not care
        }

        void SetVertexTriangles() {
            RectangleNode<CdtTriangle> triangleTree =
                RectangleNode<CdtTriangle>.CreateRectangleNodeOnEnumeration(
                    Cdt.GetTriangles().Select(t => new RectangleNode<CdtTriangle>(t, t.BoundingBox())));
            RectangleNode<SdVertex> vertexTree =
                RectangleNode<SdVertex>.CreateRectangleNodeOnEnumeration(
                    vertexArray.Select(v => new RectangleNode<SdVertex>(v, new Rectangle(v.Point))));

            RectangleNodeUtils.CrossRectangleNodes(triangleTree, vertexTree, TryToAssigenTriangleToVertex);
            foreach (SdVertex v in vertexArray) {
                Debug.Assert(v.Triangle != null);
            }
        }

        void TryToAssigenTriangleToVertex(CdtTriangle triangle, SdVertex vertex) {
            if (vertex.Triangle != null)
            {
                return;
            }

            if (Cdt.PointIsInsideOfTriangle(vertex.Point, triangle))
            {
                vertex.Triangle = triangle;
            }
        }

        internal static double CapacityOverflowPenaltyMultiplier(BundlingSettings bundlingSettings) => bundlingSettings.CapacityOverflowCoefficient * (bundlingSettings.PathLengthImportance + bundlingSettings.InkImportance);

        /// <summary>
        /// compute cdt edges crossed by paths
        /// </summary>
        internal void FillCrossedCdtEdges(Dictionary<EdgeGeometry, Set<CdtEdge>> crossedCdtEdges) {
            foreach (EdgeGeometry geometryEdge in EdgeGeometries) {
                SetPortVerticesAndObstacles(geometryEdge.SourcePort, true, out sourceLoosePoly);
                SetPortVerticesAndObstacles(geometryEdge.TargetPort, false, out targetLoosePoly);

                //crossedCdtEdges.Add(geometryEdge, new Set<CdtEdge>());
                foreach (SdBoneEdge boneEdge in EdgesToRoutes[geometryEdge]) {
                    foreach (CdtEdge cdtEdge in CrossedCdtEdgesOfBoneEdge(boneEdge)) {
                        if (AdjacentToSourceOrTarget(cdtEdge))
                        {
                            continue;
                        }

                        CollectionUtilities.AddToMap(crossedCdtEdges, geometryEdge, cdtEdge);
                    }
                }
            }
        }
    }
}
