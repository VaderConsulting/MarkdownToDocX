using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AGL.Core;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Core.Routing;
using Microsoft.AGL.Layout.LargeGraphLayout;
using Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation;
using Microsoft.AGL.Routing.Spline.Bundling;
using Microsoft.AGL.Routing.Spline.ConeSpanner;
using Microsoft.AGL.Routing.Visibility;
using Microsoft.AGL.Core.Geometry.RTree;



#if DEBUG && TEST_MSAGL
using Microsoft.AGL.DebugHelpers;
using System.Diagnostics.CodeAnalysis;
#endif

namespace Microsoft.AGL.Routing {
    ///<summary>
    /// routing splines around shapes
    ///</summary>
    public class SplineRouter : AlgorithmBase {
        /// <summary>
        /// setting this to true forces the calculation to go on even when node overlaps are present
        /// </summary>
        /// 
        bool continueOnOverlaps = true;
        public bool ContinueOnOverlaps { get => continueOnOverlaps; set => continueOnOverlaps = value; }

        Shape[] rootShapes;
        IEnumerable<EdgeGeometry> edgeGeometriesEnumeration => _edges.Select(e => e.EdgeGeometry);
        double coneAngle;
        readonly double tightPadding;
        double LoosePadding { get; set; }
        bool rootWasCreated;
        Shape root;
        VisibilityGraph visGraph;
        Dictionary<Shape, Set<Shape>> ancestorSets;
        readonly Dictionary<Shape, TightLooseCouple> shapesToTightLooseCouples = [];
        Dictionary<Port, Shape> portsToShapes;
        Dictionary<Port, Set<Shape>> portsToEnterableShapes;


        RTree<Point> portRTree;
        readonly Dictionary<Point, Polyline> portLocationsToLoosePolylines = [];
        Shape looseRoot;
        internal BundlingSettings BundlingSettings { get; set; }
        Dictionary<EdgeGeometry, Set<Polyline>> enterableLoose;
        Dictionary<EdgeGeometry, Set<Polyline>> enterableTight;
        

        readonly GeometryGraph geometryGraph;
        double multiEdgesSeparation=5;
        bool routeMultiEdgesAsBundles = true;
        internal bool UseEdgeLengthMultiplier;

        /// <summary>
        /// if set to true the algorithm will try to shortcut a shortest polyline inner points
        /// </summary>
        public bool UsePolylineEndShortcutting = true;
        /// <summary>
        /// if set to true the algorithm will try to shortcut a shortest polyline start and end
        /// </summary>
        public bool UseInnerPolylingShortcutting = true;

        internal bool AllowedShootingStraightLines = true;
        
        internal double MultiEdgesSeparation { get => multiEdgesSeparation; set => multiEdgesSeparation = value;
        }

        /// <summary>
        /// Creates a spline group router for the given graph.
        /// </summary>
        public SplineRouter(GeometryGraph graph, EdgeRoutingSettings edgeRoutingSettings) :
            this(
            graph, edgeRoutingSettings.Padding, edgeRoutingSettings.PolylinePadding, edgeRoutingSettings.ConeAngle,
            edgeRoutingSettings.BundlingSettings) {}


        /// <summary>
        /// Creates a spline group router for the given graph.
        /// </summary>
        public SplineRouter(GeometryGraph graph, double tightTightPadding, double loosePadding, double coneAngle) :
            this(graph, graph.Edges, tightTightPadding, loosePadding, coneAngle, null) {
        }

        /// <summary>
        /// Creates a spline group router for the given graph
        /// </summary>
        public SplineRouter(GeometryGraph graph, double tightTightPadding, double loosePadding, double coneAngle, BundlingSettings bundlingSettings) :
            this(graph, graph.Edges, tightTightPadding, loosePadding, coneAngle, bundlingSettings) {            
        }


        /// <summary>
        /// Creates a spline group router for the given graph.
        /// </summary>
        public SplineRouter(GeometryGraph graph, IEnumerable<Edge> edges, double tightPadding, double loosePadding, double coneAngle, BundlingSettings bundlingSettings) {
            ValidateArg.IsNotNull(graph, "graph");
            ValidateArg.IsPositive(tightPadding, "tightPadding");
            ValidateArg.IsPositive(loosePadding, "loosePadding");
            ValidateArg.IsNotNull(edges, "edges");
#if TEST_MSAGL
            // do not run the following check, cluster containment may be incorrect in float mode - need to make sure we handle this
            //graph.CheckClusterConsistency();
#endif

            _edges = edges.ToArray();

            BundlingSettings=bundlingSettings;            
            geometryGraph = graph;
            LoosePadding = loosePadding;
            this.tightPadding = tightPadding;
            IEnumerable<Shape> obstacles = ShapeCreator.GetShapes(geometryGraph);
            Initialize(obstacles, coneAngle);
        }

        readonly Edge[] _edges;

        internal Action<Edge> ReplaceEdgeByRails;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="tightPadding"></param>
        /// <param name="loosePadding"></param>
        /// <param name="coneAngle"></param>
        /// <param name="inParentEdges"></param>
        /// <param name="outParentEdges"></param>
        public SplineRouter(GeometryGraph graph, double tightPadding, double loosePadding, double coneAngle, List<Edge> inParentEdges, List<Edge> outParentEdges) {
#if TEST_MSAGL
            graph.CheckClusterConsistency();
#endif
            geometryGraph = graph;
            LoosePadding = loosePadding;
            this.tightPadding = tightPadding;
            IEnumerable<Shape> obstacles = ShapeCreatorForRoutingToParents.GetShapes(inParentEdges, outParentEdges);
            Initialize(obstacles, coneAngle);
        }

        void Initialize(IEnumerable<Shape> obstacles,
                        double coneAngleValue) {
            rootShapes = obstacles.Where(s => s.Parents == null || !s.Parents.Any()).ToArray();
            coneAngle = coneAngleValue;
            if (coneAngle == 0)
            {
                coneAngle = Math.PI / 6;
            }
        }

        /// <summary>
        /// Executes the algorithm.
        /// </summary>
        protected override void RunInternal() {
            if (!edgeGeometriesEnumeration.Any())
            {
                return;
            }

            GetOrCreateRoot();
            RouteOnRoot();
            RemoveRoot();
       /*     var ll = new List<DebugCurve>();
            ll.AddRange(rootShapes.Select(s=>shapesToTightLooseCouples[s].TightPolyline).Select(p=>new DebugCurve(100,0.05,"black", p)));
            ll.AddRange(geometryGraph.Edges.Select(s => new DebugCurve(100, 0.05, "black", s.Curve)));               
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration (ll);
            LayoutAlgorithmSettings.ShowGraph(geometryGraph);*/
        }
        void RouteOnRoot() {
            CalculatePortsToShapes();
            CalculatePortsToEnterableShapes();
            CalculateShapeToBoundaries(root);
            if (OverlapsDetected && !ContinueOnOverlaps)
            {
                return;
            }

            BindLooseShapes();
            SetLoosePolylinesForAnywherePorts();
            CalculateVisibilityGraph();
            RouteOnVisGraph();
        }

      
        /*
        IEnumerable<Polyline> AllPolysDeb() {
            foreach (var rootShape in looseRoot.Children) {
                foreach (var poly in PolysDebugUnderShape(rootShape)) {
                    yield return poly;
                }
            }
        }

        IEnumerable<Polyline> PolysDebugUnderShape(Shape shape) {
            yield return (Polyline)shape.BoundaryCurve;
            foreach (var child in shape.Children) {
                foreach (var poly in PolysDebugUnderShape(child)) {
                    yield return poly;
                }
            }
        }
        */
        void CalculatePortsToEnterableShapes() {
            portsToEnterableShapes = [];
            foreach (KeyValuePair<Port, Shape> portsToShape in portsToShapes) {
                Port port = portsToShape.Key;
                Set<Shape> set = new Set<Shape>();
                if (!(EdgesAttachedToPortAvoidTheNode(port)))
                {
                    set.Insert(portsToShape.Value);
                }

                portsToEnterableShapes[port] = set;
            }

            foreach (Shape rootShape in rootShapes) {
                foreach(Shape sh in rootShape.Descendants)
                {
                    foreach (Port port in sh.Ports) {
                        Set<Shape> enterableSet = portsToEnterableShapes[port];
                        enterableSet.InsertRange(sh.Ancestors.Where(s => s.BoundaryCurve != null));
                    }
                }
            }
        }


        static bool EdgesAttachedToPortAvoidTheNode(Port port) => port is CurvePort || port is ClusterBoundaryPort;

        void SetLoosePolylinesForAnywherePorts() {
            foreach (KeyValuePair<Shape, TightLooseCouple> shapesToTightLooseCouple in shapesToTightLooseCouples) {
                Shape shape = shapesToTightLooseCouple.Key;
                foreach (Port port in shape.Ports) {
                    if (port is HookUpAnywhereFromInsidePort aport)
                    {
                        aport.LoosePolyline = (Polyline)shapesToTightLooseCouple.Value.LooseShape.BoundaryCurve;
                    }

                    if (port is ClusterBoundaryPort clusterBoundaryPort)
                    {
                        clusterBoundaryPort.LoosePolyline = (Polyline)shapesToTightLooseCouple.Value.LooseShape.BoundaryCurve;
                    }
                }
            }
        }

        void BindLooseShapes() {
            looseRoot = new Shape();
#if TEST_MSAGL
            looseRoot.UserData = (string)root.UserData + "x";
#endif
            foreach (Shape shape in root.Children) {
                Shape looseShape = shapesToTightLooseCouples[shape].LooseShape;
                BindLooseShapesUnderShape(shape);
                looseRoot.AddChild(looseShape);
            }
        }

        void BindLooseShapesUnderShape(Shape shape) {
            Shape loose = shapesToTightLooseCouples[shape].LooseShape;
            foreach (Shape child in shape.Children) {
                Shape childLooseShape = shapesToTightLooseCouples[child].LooseShape;
                loose.AddChild(childLooseShape);
                BindLooseShapesUnderShape(child);
            }
        }




        void CalculateShapeToBoundaries(Shape shape) {
            ProgressStep();
            if (!shape.Children.Any())
            {
                return;
            }

            foreach (Shape child in shape.Children)
            {
                CalculateShapeToBoundaries(child);
            }

            ShapeObstacleCalculator obstacleCalculator = new ShapeObstacleCalculator(shape, tightPadding, AdjustedLoosePadding,
                                                                 shapesToTightLooseCouples);
            obstacleCalculator.Calculate();

#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=370
            //SharpKit/Colin - not supported directly!
            OverlapsDetected = OverlapsDetected | obstacleCalculator.OverlapsDetected;
#else
            OverlapsDetected |= obstacleCalculator.OverlapsDetected;
#endif
        }
        /// <summary>
        /// set to true if and only if there are overlaps in tight obstacles
        /// </summary>
        public bool OverlapsDetected { get; set; }

        internal double AdjustedLoosePadding => BundlingSettings == null ? LoosePadding : LoosePadding * BundleRouter.SuperLoosePaddingCoefficient;


        void RouteOnVisGraph() {
            ancestorSets = GetAncestorSetsMap(root.Descendants);
            if (BundlingSettings == null) {
                foreach (IGrouping<Set<Shape>, Edge> edgeGroup in _edges.GroupBy(EdgePassport)) {
                    Set<Shape> passport = edgeGroup.Key;
                    Set<Shape> obstacleShapes = GetObstaclesFromPassport(passport);
                    InteractiveEdgeRouter interactiveEdgeRouter = CreateInteractiveEdgeRouter(obstacleShapes);
                    RouteEdgesWithTheSamePassport(edgeGroup, interactiveEdgeRouter, obstacleShapes);
                }
            }
            else
            {
                RouteBundles();
            }
        }


        void RouteEdgesWithTheSamePassport(IGrouping<Set<Shape>, Edge> edgeGeometryGroup, InteractiveEdgeRouter interactiveEdgeRouter, Set<Shape> obstacleShapes) {

            if (RouteMultiEdgesAsBundles)
            {
                SplitOnRegularAndMultiedges(edgeGeometryGroup, out List<Edge> regularEdges, out List<Edge[]> multiEdges);
                foreach (Edge edge in regularEdges)
                {
                    RouteEdge(interactiveEdgeRouter, edge);
                }

                if (multiEdges != null)
                {
                    ScaleDownLooseHierarchy(interactiveEdgeRouter, obstacleShapes);
                    RouteMultiEdges(multiEdges, interactiveEdgeRouter, edgeGeometryGroup.Key);
                }
            }
            else
            {
                foreach (Edge eg in edgeGeometryGroup)
                {
                    RouteEdge(interactiveEdgeRouter, eg);
                }
            }
        }

        /// <summary>
        /// if set to true routes multi edges as ordered bundles
        /// </summary>
        public bool RouteMultiEdgesAsBundles { get => routeMultiEdgesAsBundles; set => routeMultiEdgesAsBundles = value;
        }

        void RouteEdge(InteractiveEdgeRouter interactiveEdgeRouter, Edge edge) {
            List<Shape> transparentShapes = MakeTransparentShapesOfEdgeGeometryAndGetTheShapes(edge.EdgeGeometry);
            ProgressStep();
            RouteEdgeGeometry(edge, interactiveEdgeRouter);
            SetTransparency(transparentShapes, false);
        }

        void ScaleDownLooseHierarchy(InteractiveEdgeRouter interactiveEdgeRouter, Set<Shape> obstacleShapes) {
            List<Polyline> loosePolys = new List<Polyline>();
            foreach (Shape obstacleShape in obstacleShapes) {
                TightLooseCouple tl = shapesToTightLooseCouples[obstacleShape];
                loosePolys.Add( InteractiveObstacleCalculator.LoosePolylineWithFewCorners(tl.TightPolyline, tl.Distance / BundleRouter.SuperLoosePaddingCoefficient));
            }

            interactiveEdgeRouter.LooseHierarchy=CreateLooseObstacleHierarachy(loosePolys);
                
            interactiveEdgeRouter.ClearActivePolygons();

            interactiveEdgeRouter.AddActivePolygons(loosePolys.Select(polyline => new Polygon(polyline)));         
        }

        
        void RouteMultiEdges(List<Edge[]> multiEdges, InteractiveEdgeRouter interactiveEdgeRouter, Set<Shape> parents) {
            MultiEdgeRouter mer = new MultiEdgeRouter(multiEdges, interactiveEdgeRouter, parents.SelectMany(p => p.Children).Select(s => s.BoundaryCurve), 
                 new BundlingSettings { InkImportance = 0.00001, EdgeSeparation = MultiEdgesSeparation}, MakeTransparentShapesOfEdgeGeometryAndGetTheShapes);
            //giving more importance to ink might produce weird routings with huge detours, maybe 0 is the best value here
            mer.Run();
           
        }

        

//        void ScaleLoosePolylinesOfInvolvedShapesDown(Set<Shape> parents) {        
//            foreach (var parent in parents) {
//                foreach (var shape in parent.Descendands) {
//                    TightLooseCouple tl = this.shapesToTightLooseCouples[shape];
//                    tl.LooseShape.BoundaryCurveBackup = tl.LooseShape.BoundaryCurve;
//                    tl.LooseShape.BoundaryCurve = InteractiveObstacleCalculator.LoosePolylineWithFewCorners(tl.TightPolyline, tl.Distance / BundleRouter.SuperLoosePaddingCoefficient);
//                }
//            }
//        }
//
//        void RestoreLoosePolylinesOfInvolvedShapes(Set<Shape> parents) {
//            foreach (var parent in parents) {
//                foreach (var shape in parent.Descendands) {
//                    TightLooseCouple tl = shapesToTightLooseCouples[shape];
//                    tl.LooseShape.BoundaryCurve = tl.LooseShape.BoundaryCurveBackup;
//                }
//            }
//        }

        void SplitOnRegularAndMultiedges(IEnumerable<Edge> edges, out List<Edge> regularEdges, out List<Edge[]> multiEdges) {

            regularEdges = [];

            Dictionary<PointPair, List<Edge>> portLocationPairsToEdges = new Dictionary<PointPair, List<Edge>>();
            foreach (Edge eg in edges) {
                if (IsEdgeToParent(eg.EdgeGeometry))
                {
                    regularEdges.Add(eg);
                }
                else
                {
                    RegisterInPortLocationsToEdges(eg, portLocationPairsToEdges);
                }
            }

            multiEdges = null;

            foreach (List<Edge> edgeGroup in portLocationPairsToEdges.Values) {
                if (edgeGroup.Count == 1 ||  OverlapsDetected)
                {
                    regularEdges.AddRange(edgeGroup);
                }
                else {
                    if (multiEdges == null)
                    {
                        multiEdges = [];
                    }

                    multiEdges.Add(edgeGroup.ToArray());
                }
            }           
        }

        static void RegisterInPortLocationsToEdges(Edge eg, Dictionary<PointPair, List<Edge>> portLocationPairsToEdges) {
            PointPair pp = new PointPair(eg.SourcePort.Location, eg.TargetPort.Location);
            if (!portLocationPairsToEdges.TryGetValue(pp, out List<Edge> list))
            {
                portLocationPairsToEdges[pp] = list = [];
            }

            list.Add(eg);
        }

        static bool IsEdgeToParent(EdgeGeometry e) => e.SourcePort is HookUpAnywhereFromInsidePort || e.TargetPort is HookUpAnywhereFromInsidePort;

        InteractiveEdgeRouter CreateInteractiveEdgeRouter(IEnumerable<Shape> obstacleShapes) {
            //we need to create a set here because one loose polyline can hold several original shapes
            Set<Polyline> loosePolys = new Set<Polyline>(obstacleShapes.Select(sh => shapesToTightLooseCouples[sh].LooseShape.BoundaryCurve as Polyline));
            InteractiveEdgeRouter router = new InteractiveEdgeRouter {
                VisibilityGraph = visGraph,
                TightHierarchy =
                    CreateTightObstacleHierarachy(obstacleShapes),
                LooseHierarchy =
                    CreateLooseObstacleHierarachy(loosePolys),
                UseSpanner = true,
                LookForRoundedVertices = true,
                TightPadding = tightPadding,
                LoosePadding = LoosePadding,
                UseEdgeLengthMultiplier = UseEdgeLengthMultiplier,
                UsePolylineEndShortcutting = UsePolylineEndShortcutting,
                UseInnerPolylingShortcutting = UseInnerPolylingShortcutting,
                AllowedShootingStraightLines = AllowedShootingStraightLines,
                CacheCorners = CacheCornersForSmoothing,
            };

            router.AddActivePolygons(loosePolys.Select(polyline => new Polygon(polyline)));
            return router;
        }
        /// <summary>
        /// 
        /// </summary>
        public bool CacheCornersForSmoothing { get; set; }

        Set<Shape> GetObstaclesFromPassport(Set<Shape> passport) {
            if (passport.Count == 0)
            {
                return new Set<Shape>(root.Children);
            }

            Set<Shape> commonAncestors = GetCommonAncestorsAbovePassport(passport);
            Set<Shape> allAncestors = GetAllAncestors(passport);
            Set<Shape> ret = new Set<Shape>(passport.SelectMany(p => p.Children.Where(child => !allAncestors.Contains(child))));
            Set<Shape> enqueued = new Set<Shape>(passport.Concat(ret));
            Queue<Shape> queue = new Queue<Shape>();
            foreach (Shape shape in passport.Where(shape => !commonAncestors.Contains(shape)))
            {
                queue.Enqueue(shape);
            }

            while (queue.Count > 0) {
                Shape a = queue.Dequeue();
                foreach (Shape parent in a.Parents) {
                    foreach (Shape sibling in parent.Children)
                    {
                        if (!allAncestors.Contains(sibling))
                        {
                            ret.Insert(sibling);
                        }
                    }

                    if (!commonAncestors.Contains(parent) && !enqueued.Contains(parent)) {
                        queue.Enqueue(parent);
                        enqueued.Insert(parent);
                    }
                }
            }
            return ret;
        }

        Set<Shape> GetAllAncestors(Set<Shape> passport) {
            if (!passport.Any())
            {
                return [];
            }

            Set<Shape> ret = new Set<Shape>(passport);
            foreach (Shape shape in passport)
            {
                ret += ancestorSets[shape];
            }

            return ret;
        }

        Set<Shape> GetCommonAncestorsAbovePassport(Set<Shape> passport) {
            if (!passport.Any())
            {
                return [];
            }

            Set<Shape> ret = ancestorSets[passport.First()];
            foreach (Shape shape in passport.Skip(1))
            {
                ret *= ancestorSets[shape];
            }

            return ret;
        }

        void RouteBundles() {
            ScaleLooseShapesDown();

            CalculateEdgeEnterablePolylines();
            RectangleNode<Polyline> looseHierarchy = GetLooseHierarchy();
            Cdt cdt = BundleRouter.CreateConstrainedDelaunayTriangulation(looseHierarchy);
            // CdtSweeper.ShowFront(cdt.GetTriangles(), null, null,this.visGraph.Edges.Select(e=>new LineSegment(e.SourcePoint,e.TargetPoint)));

            SdShortestPath shortestPath = new SdShortestPath(MakeTransparentShapesOfEdgeGeometryAndGetTheShapes, cdt, FindCdtGates(cdt));
            BundleRouter bundleRouter = new BundleRouter(geometryGraph, shortestPath, visGraph, BundlingSettings,
                                                         LoosePadding, GetTightHierarchy(),
                                                         looseHierarchy, enterableLoose, enterableTight,
                                                         port=>LoosePolyOfOriginalShape(portsToShapes[port]));

            bundleRouter.Run();
        }

        void CreateTheMapToParentLooseShapes(Shape shape, Dictionary<ICurve, Shape> loosePolylinesToLooseParentShapeMap) {
            foreach (Shape childShape in shape.Children) {
                TightLooseCouple tightLooseCouple = shapesToTightLooseCouples[childShape];
                ICurve poly = tightLooseCouple.LooseShape.BoundaryCurve;
                loosePolylinesToLooseParentShapeMap[poly] = shape;
                CreateTheMapToParentLooseShapes(childShape, loosePolylinesToLooseParentShapeMap);
            }
        }

        Set<CdtEdge> FindCdtGates(Cdt cdt) {
            Dictionary<ICurve, Shape> loosePolylinesToLooseParentShapeMap = [];
        
            CreateTheMapToParentLooseShapes(root, loosePolylinesToLooseParentShapeMap);
            //looking for Cdt edges connecting two siblings; only those we define as gates
            Set<CdtEdge> gates = new Set<CdtEdge>();
            foreach (CdtSite cdtSite in cdt.PointsToSites.Values) {
                foreach (CdtEdge cdtEdge in cdtSite.Edges) {
                    
                    if(cdtEdge.CwTriangle==null && cdtEdge.CcwTriangle==null)
                    {
                        continue;
                    }

                    Polyline a = (Polyline)cdtSite.Owner;
                    Polyline b = (Polyline)cdtEdge.lowerSite.Owner;
                    if (a == b)
                    {
                        continue;
                    }

                    if (loosePolylinesToLooseParentShapeMap.TryGetValue(a, out Shape aParent)
                        && loosePolylinesToLooseParentShapeMap.TryGetValue(b, out Shape bParent) && aParent == bParent)
                    {
                        gates.Insert(cdtEdge);
                    }
                }
            }
           //CdtSweeper.ShowFront(cdt.GetTriangles(), null,
             //                    gates.Select(g => new LineSegment(g.upperSite.Point, g.lowerSite.Point)), null);
            return gates;
        }


        void CalculateEdgeEnterablePolylines() {
            enterableLoose = [];
            enterableTight = [];
            foreach (EdgeGeometry edgeGeometry in edgeGeometriesEnumeration) {
                GetEdgeEnterablePolylines(edgeGeometry, out Set<Polyline> looseSet, out Set<Polyline> tightSet);
                enterableLoose[edgeGeometry] = looseSet;
                enterableTight[edgeGeometry] = tightSet;
            }
        }

        void GetEdgeEnterablePolylines(EdgeGeometry edgeGeometry, out Set<Polyline> looseEnterable, 
            out Set<Polyline> tightEnterable) {
            looseEnterable = [];
            tightEnterable = [];
            Shape sourceShape = portsToShapes[edgeGeometry.SourcePort];
            Shape targetShape = portsToShapes[edgeGeometry.TargetPort];

            if (sourceShape != root) {
                looseEnterable.InsertRange(ancestorSets[sourceShape].Select(LoosePolyOfOriginalShape).Where(p => p != null));
                tightEnterable.InsertRange(ancestorSets[sourceShape].Select(TightPolyOfOriginalShape).Where(p => p != null));
            }

            if (targetShape != root) {
                looseEnterable.InsertRange(ancestorSets[targetShape].Select(LoosePolyOfOriginalShape).Where(p => p != null));
                tightEnterable.InsertRange(ancestorSets[targetShape].Select(TightPolyOfOriginalShape).Where(p => p != null));
            }
        }

        RectangleNode<Polyline> GetTightHierarchy() => RectangleNode<Polyline>.CreateRectangleNodeOnEnumeration(shapesToTightLooseCouples.Values.
                Select(tl => new RectangleNode<Polyline>(tl.TightPolyline, tl.TightPolyline.BoundingBox)));

        RectangleNode<Polyline> GetLooseHierarchy() {
            Set<Polyline> loosePolylines = new Set<Polyline>(shapesToTightLooseCouples.Values.Select(tl => (Polyline)(tl.LooseShape.BoundaryCurve)));
            return RectangleNode<Polyline>.CreateRectangleNodeOnEnumeration(loosePolylines.Select(p => new RectangleNode<Polyline>(p, p.BoundingBox)));
        }

        void ScaleLooseShapesDown() {
            foreach (KeyValuePair<Shape, TightLooseCouple> shapesToTightLooseCouple in shapesToTightLooseCouples) {
                TightLooseCouple tl = shapesToTightLooseCouple.Value;
                tl.LooseShape.BoundaryCurve = InteractiveObstacleCalculator.LoosePolylineWithFewCorners(tl.TightPolyline, tl.Distance / BundleRouter.SuperLoosePaddingCoefficient);
            }
        }

        /// <summary>
        ///  The set of shapes where the edgeGeometry source and target ports shapes are citizens.
        ///  In the simple case it is the union of the target port shape parents and the sourceport shape parents.
        ///  When one end shape contains another, the passport is the set consisting of the end shape and all other shape parents.
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        Set<Shape> EdgePassport(Edge edge) {
            EdgeGeometry edgeGeometry = edge.EdgeGeometry;
            Set<Shape> ret = new Set<Shape>();
            Shape sourceShape = portsToShapes[edgeGeometry.SourcePort];
            Shape targetShape = portsToShapes[edgeGeometry.TargetPort];

            if (IsAncestor(sourceShape, targetShape)) {
                ret.InsertRange(targetShape.Parents);
                ret.Insert(sourceShape);
                return ret;
            }
            if (IsAncestor(targetShape, sourceShape)) {
                ret.InsertRange(sourceShape.Parents);
                ret.Insert(targetShape);
                return ret;
            }

            if (sourceShape != looseRoot)
            {
                ret.InsertRange(sourceShape.Parents);
            }

            if (targetShape != looseRoot)
            {
                ret.InsertRange(targetShape.Parents);
            }

            return ret;
        }

        IEnumerable<Port> AllPorts() {
            foreach (EdgeGeometry edgeGeometry in edgeGeometriesEnumeration) {
                yield return edgeGeometry.SourcePort;
                yield return edgeGeometry.TargetPort;
            }
        }
            
        void CalculatePortsToShapes() {
            portsToShapes = [];
            foreach (Shape shape in root.Descendants)
            {
                foreach (Port port in shape.Ports)
                {
                    portsToShapes[port] = shape;
                }
            }
            //assign all orphan ports to the root 
            foreach (Port port in AllPorts().Where(p => !portsToShapes.ContainsKey(p))) {
                root.Ports.Insert(port);
                portsToShapes[port] = root;
            }
        }



        void RouteEdgeGeometry(Edge edge, InteractiveEdgeRouter iRouter) {
            EdgeGeometry edgeGeometry = edge.EdgeGeometry;

            List<VisibilityEdge> addedEdges = new List<VisibilityEdge>();
            if (!(edgeGeometry.SourcePort is HookUpAnywhereFromInsidePort))
            {
                addedEdges.AddRange(AddVisibilityEdgesFromPort(edgeGeometry.SourcePort));
            }

            if (!(edgeGeometry.TargetPort is HookUpAnywhereFromInsidePort))
            {
                addedEdges.AddRange(AddVisibilityEdgesFromPort(edgeGeometry.TargetPort));
            }

            SmoothedPolyline smoothedPolyline;
            if (!ApproximateComparer.Close(edgeGeometry.SourcePort.Location, edgeGeometry.TargetPort.Location))
            {
                edgeGeometry.Curve = iRouter.RouteSplineFromPortToPortWhenTheWholeGraphIsReady(
                edgeGeometry.SourcePort, edgeGeometry.TargetPort, true, out smoothedPolyline);
            }
            else {
                edgeGeometry.Curve = Edge.RouteSelfEdge(edgeGeometry.SourcePort.Curve, Math.Max(LoosePadding * 2, edgeGeometry.GetMaxArrowheadLength()), out smoothedPolyline);
            }
            edgeGeometry.SmoothedPolyline = smoothedPolyline;
            
            if (edgeGeometry.Curve == null)
            {
                throw new NotImplementedException();
            }

            foreach (VisibilityEdge visibilityEdge in addedEdges)
            {
                VisibilityGraph.RemoveEdge(visibilityEdge);
            }

            Arrowheads.TrimSplineAndCalculateArrowheads(edgeGeometry, edgeGeometry.SourcePort.Curve,
                                                        edgeGeometry.TargetPort.Curve, edgeGeometry.Curve,
                                                        false, KeepOriginalSpline);
            if (ReplaceEdgeByRails != null)
            {
                ReplaceEdgeByRails(edge);
            }
            //  SetTransparency(transparentShapes, false);
        }
        /// <summary>
        /// if set to true the original spline is kept under the corresponding EdgeGeometry
        /// </summary>
        public bool KeepOriginalSpline {get; set; }

        /// <summary>
        /// 
        /// </summary>
        public double ArrowHeadRatio { get; set; }

        internal Point[] LineSweeperPorts { get; set; }

        /// <summary>
        /// 
        /// </summary>
        
        
        IEnumerable<VisibilityEdge> AddVisibilityEdgesFromPort(Port port) {

            if (port is CurvePort || !portsToShapes.TryGetValue(port, out Shape portShape) ||
                !shapesToTightLooseCouples.TryGetValue(portShape, out TightLooseCouple boundaryCouple))
            {
                return new VisibilityEdge[] { };
            }

            Shape portLoosePoly = boundaryCouple.LooseShape;
            return (from point in portLoosePoly.BoundaryCurve as Polyline
                    where visGraph.FindEdge(port.Location, point) == null
                    select visGraph.AddEdge(port.Location, point));
        }
        List<Shape> MakeTransparentShapesOfEdgeGeometryAndGetTheShapes(EdgeGeometry edgeGeometry) {
            //it is OK here to repeat a shape in the returned list
            Shape sourceShape = portsToShapes[edgeGeometry.SourcePort];
            Shape targetShape = portsToShapes[edgeGeometry.TargetPort];

            List<Shape> transparentLooseShapes = new List<Shape>();

            foreach (Shape shape in GetTransparentShapes(edgeGeometry.SourcePort, edgeGeometry.TargetPort, sourceShape, targetShape).ToArray())
            {
                if (shape != null)
                {
                    transparentLooseShapes.Add(LooseShapeOfOriginalShape(shape));
                }
            }

            foreach (Shape shape in portsToEnterableShapes[edgeGeometry.SourcePort])
            {
                transparentLooseShapes.Add(LooseShapeOfOriginalShape(shape));
            }

            foreach (Shape shape in portsToEnterableShapes[edgeGeometry.TargetPort])
            {
                transparentLooseShapes.Add(LooseShapeOfOriginalShape(shape));
            }

            SetTransparency(transparentLooseShapes, true);
            return transparentLooseShapes;
        }

        Shape LooseShapeOfOriginalShape(Shape s) {
            if (s == root)
            {
                return looseRoot;
            }

            return shapesToTightLooseCouples[s].LooseShape;
        }

        Polyline LoosePolyOfOriginalShape(Shape s) => (Polyline)(LooseShapeOfOriginalShape(s).BoundaryCurve);
        Polyline TightPolyOfOriginalShape(Shape s) {
            if (s == root)
            {
                return null;
            }

            return shapesToTightLooseCouples[s].TightPolyline;
        }
        #region debugging

#if DEBUG && TEST_MSAGL
        internal static void AnotherShowMethod(VisibilityGraph visGraph,
            Port sourcePort, Port targetPort, IEnumerable<Shape> obstacleShapes, ICurve curve) {
            List<DebugCurve> dd = new List<DebugCurve>(
         visGraph.Edges.Select(
            e =>
            new DebugCurve(100, 0.1, GetEdgeColor(e, sourcePort, targetPort),
                           new LineSegment(e.SourcePoint, e.TargetPoint))));
            if (obstacleShapes != null)
            {
                dd.AddRange(
                    obstacleShapes.Select(s => new DebugCurve(1, s.BoundaryCurve)));
            }

            if (sourcePort != null && targetPort != null)
            {
                dd.AddRange(new[] {
                                    new DebugCurve(CurveFactory.CreateDiamond(3, 3, sourcePort.Location)),
                                    new DebugCurve(CurveFactory.CreateEllipse(3, 3, targetPort.Location)),
                                });
            }

            if (curve != null)
            {
                dd.Add(new DebugCurve(5, "purple", curve));
            }

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dd);
        }


        static string GetEdgeColor(VisibilityEdge e, Port sourcePort, Port targetPort) {
            if (sourcePort == null || targetPort == null)
            {
                return "green";
            }

            if (ApproximateComparer.Close(e.SourcePoint, sourcePort.Location) ||
                ApproximateComparer.Close(e.SourcePoint, targetPort.Location)
                || ApproximateComparer.Close(e.TargetPoint, sourcePort.Location) ||
                ApproximateComparer.Close(e.TargetPoint, targetPort.Location))
            {
                return "lightgreen";
            }

            return e.IsPassable == null || e.IsPassable() ? "green" : "red";
        }
#endif

        #endregion

        IEnumerable<Shape> GetTransparentShapes(
            Port sourcePort,
            Port targetPort, Shape sourceShape, Shape targetShape) {
            foreach (Shape s in ancestorSets[sourceShape])
            {
                yield return s;
            }

            foreach (Shape s in ancestorSets[targetShape])
            {
                yield return s;
            }

            bool routingOutsideOfSourceBoundary = EdgesAttachedToPortAvoidTheNode(sourcePort);
            bool routingOutsideOfTargetBoundary = EdgesAttachedToPortAvoidTheNode(targetPort);
            if (!routingOutsideOfSourceBoundary && !routingOutsideOfTargetBoundary) {
                yield return sourceShape;
                yield return targetShape;
            } else if (routingOutsideOfSourceBoundary) {
                if (IsAncestor(sourceShape, targetShape))
                {
                    yield return sourceShape;
                }
            }
            else {
                if (IsAncestor(targetShape, sourceShape))
                {
                    yield return targetShape;
                }
            }
        }

        static void SetTransparency(IEnumerable<Shape> shapes, bool v) {
            foreach (Shape shape in shapes)
            {
                shape.IsTransparent = v;
            }
        }


        /*
        /// <summary>
        /// it is the set of parents with all children being obstacle candidates
        /// </summary>
        /// <param name="port"></param>
        /// <param name="shape"></param>
        /// <param name="otherShape"></param>
        /// <returns></returns>
        IEnumerable<Shape> FindMinimalParents(Port port, Shape shape, Shape otherShape) {
            if (shape == null)
                yield return rootOfLooseShapes;
            else if (port is CurvePort) {
                if (IsLgAncestor(shape, otherShape))
                    yield return shape;
                else
                    foreach (Shape parent in shape.Parents)
                        yield return parent;
            } else if (PortIsInsideOfShape(port, shape) && shape.Children.Any())
                yield return shape;
            else
                foreach (Shape parent in shape.Parents)
                    yield return parent;
        }
*/

        bool IsAncestor(Shape possibleAncestor, Shape possiblePredecessor) => possiblePredecessor != null &&
                   ancestorSets.TryGetValue(possiblePredecessor, out Set<Shape> ancestors) && ancestors != null &&
                   ancestors.Contains(possibleAncestor);

        /*
                static bool PortIsInsideOfShape(Port port, Shape shape) {
                    if (shape == null)
                        return false;
                    return Curve.PointRelativeToCurveLocation(ApproximateComparer.Round(port.Location), shape.BoundaryCurve) ==
                           PointLocation.Inside;
                }
        */

        static RectangleNode<Polyline> CreateLooseObstacleHierarachy(IEnumerable<Polyline> loosePolys) => RectangleNode<Polyline>.CreateRectangleNodeOnEnumeration(
                    loosePolys.Select(
                        poly => new RectangleNode<Polyline>(poly, poly.BoundingBox)));

        RectangleNode<Polyline> CreateTightObstacleHierarachy(IEnumerable<Shape> obstacles) {
            IEnumerable<Polyline> tightPolys = obstacles.Select(sh => shapesToTightLooseCouples[sh].TightPolyline);
            return
                RectangleNode<Polyline>.CreateRectangleNodeOnEnumeration(
                    tightPolys.Select(tightPoly => new RectangleNode<Polyline>(
                                                       tightPoly,
                                                       tightPoly.BoundingBox)));
        }

        void CalculateVisibilityGraph() {
            Set<Point> setOfPortLocations = LineSweeperPorts != null ? new Set<Point>(LineSweeperPorts) : [];
            foreach (EdgeGeometry edgeGeometry in edgeGeometriesEnumeration) {
                if (!(edgeGeometry.SourcePort is HookUpAnywhereFromInsidePort || edgeGeometry.SourcePort is ClusterBoundaryPort))
                {
                    setOfPortLocations.Insert(edgeGeometry.SourcePort.Location);
                }

                if (!(edgeGeometry.TargetPort is HookUpAnywhereFromInsidePort || edgeGeometry.TargetPort is ClusterBoundaryPort))
                {
                    setOfPortLocations.Insert(edgeGeometry.TargetPort.Location);
                }
            }
            portRTree =
                new RTree<Point>(
                    setOfPortLocations.Select(p => new KeyValuePair<Rectangle, Point>(new Rectangle(p), p)));
            visGraph = new VisibilityGraph();
            
            FillVisibilityGraphUnderShape(root);
//            ShowVisGraph(visGraph, new Set<Polyline>(shapesToTightLooseCouples.Values.Select(tl => (Polyline)(tl.LooseShape.BoundaryCurve))),
//                geometryGraph.Nodes.Select(n => n.BoundaryCurve).Concat(root.Descendands.Select(d => d.BoundaryCurve)), null);
        }

        /// <summary>
        /// this function might change the shape's loose polylines by inserting new points
        /// </summary>
        void FillVisibilityGraphUnderShape(Shape shape) {
            //going depth first 
            IEnumerable<Shape> children = shape.Children;
            foreach (Shape child in children)
            {
                FillVisibilityGraphUnderShape(child);
            }

            Polyline looseBoundary = shapesToTightLooseCouples.TryGetValue(shape, out TightLooseCouple tightLooseCouple) ? tightLooseCouple.LooseShape.BoundaryCurve as Polyline : null;
            Shape looseShape = tightLooseCouple != null ? tightLooseCouple.LooseShape : looseRoot;
            Set<Polyline> obstacles = new Set<Polyline>(looseShape.Children.Select(c => c.BoundaryCurve as Polyline));

            Set<Point> portLocations = RemoveInsidePortsAndSplitBoundaryIfNeeded(looseBoundary);
            //this run will split the polyline enough to route later from the inner ports
            VisibilityGraph tmpVisGraph = new VisibilityGraph();
            ConeSpanner coneSpanner = new ConeSpanner(new Polyline[] {}, tmpVisGraph, coneAngle, portLocations, looseBoundary);
            coneSpanner.Run();
            //now run the spanner again to create the correct visibility graph around the inner obstacles
            tmpVisGraph = new VisibilityGraph();
            coneSpanner = new ConeSpanner(obstacles, tmpVisGraph, coneAngle, portLocations, looseBoundary) {
                Bidirectional = Bidirectional && obstacles.Count>0
            };
            coneSpanner.Run();

            ProgressStep();

            foreach (VisibilityEdge edge in tmpVisGraph.Edges)
            {
                TryToCreateNewEdgeAndSetIsPassable(edge, looseShape);
            }

            AddBoundaryEdgesToVisGraph(looseBoundary);
//            if (obstacles.Count > 0)
//                SplineRouter.ShowVisGraph(tmpVisGraph, obstacles, null, null);
        }

        /// <summary>
        /// If set to true then a smaller visibility graph is created.
        /// An edge is added to the visibility graph only if it is found at least twice: 
        /// once sweeping with a direction d and the second time with -d
        /// </summary>
        internal bool Bidirectional { get; set; }

#if TEST_MSAGL
        internal static void ShowVisGraph(VisibilityGraph tmpVisGraph, IEnumerable<Polyline> obstacles, IEnumerable<ICurve> greenCurves, IEnumerable<ICurve> redCurves) {
            List<DebugCurve> l = new List<DebugCurve>(tmpVisGraph.Edges.Select(e => new DebugCurve(100, 1,
                e.IsPassable != null && e.IsPassable() ? "green" : "black"
                , new LineSegment(e.SourcePoint, e.TargetPoint))));
            if (obstacles != null)
            {
                l.AddRange(obstacles.Select(p => new DebugCurve(100, 1, "brown", p)));
            }

            if (greenCurves != null)
            {
                l.AddRange(greenCurves.Select(p => new DebugCurve(100, 10, "navy", p)));
            }

            if (redCurves != null)
            {
                l.AddRange(redCurves.Select(p => new DebugCurve(100, 10, "red", p)));
            }

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(l);
        }
#endif
        void TryToCreateNewEdgeAndSetIsPassable(VisibilityEdge edge, Shape looseShape) {
            VisibilityEdge e = visGraph.FindEdge(edge.SourcePoint, edge.TargetPoint);
            if (e != null)
            {
                return;
            }

            e = visGraph.AddEdge(edge.SourcePoint, edge.TargetPoint);
            if (looseShape != null)
            {
                e.IsPassable = () => looseShape.IsTransparent;
            }
        }


        void AddBoundaryEdgesToVisGraph(Polyline boundary) {
            if (boundary == null)
            {
                return;
            }

            PolylinePoint p = boundary.StartPoint;
            do {
                PolylinePoint pn = p.NextOnPolyline;
                visGraph.AddEdge(p.Point, pn.Point);
                if (pn == boundary.StartPoint)
                {
                    break;
                }

                p = pn;
            } while (true);
        }

        Set<Point> RemoveInsidePortsAndSplitBoundaryIfNeeded(Polyline boundary) {
            Set<Point> ret = new Set<Point>();

            if (boundary == null) {
                foreach (Point point in portRTree.GetAllLeaves())
                {
                    ret.Insert(point);
                }

                portRTree.Clear();
                return ret;
            }
            Rectangle boundaryBox = boundary.BoundingBox;
            Point[] portLocationsInQuestion = portRTree.GetAllIntersecting(boundaryBox).ToArray();
            foreach (Point point in portLocationsInQuestion) {
                switch (Curve.PointRelativeToCurveLocation(point, boundary)) {
                    case PointLocation.Inside:
                        ret.Insert(point);
                        portLocationsToLoosePolylines[point] = boundary;
                        portRTree.Remove(new Rectangle(point), point);
                        break;
                    case PointLocation.Boundary:
                        portRTree.Remove(new Rectangle(point), point);
                        portLocationsToLoosePolylines[point] = boundary;
                        PolylinePoint polylinePoint = FindPointOnPolylineToInsertAfter(boundary, point);
                        if (polylinePoint != null)
                        {
                            LineSweeper.InsertPointIntoPolylineAfter(boundary, polylinePoint, point);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                }
            }
            return ret;
        }

        static PolylinePoint FindPointOnPolylineToInsertAfter(Polyline boundary, Point point) {
            for (PolylinePoint p = boundary.StartPoint; ; ) {
                PolylinePoint pn = p.NextOnPolyline;

                if (ApproximateComparer.Close(point, p.Point) || ApproximateComparer.Close(point, pn.Point))
                {
                    return null; //the point is already inside
                }

                if (ApproximateComparer.Close(Point.DistToLineSegment(point, p.Point, pn.Point, out double par), 0))
                {
                    return p;
                }

                p = pn;
                if (p == boundary.StartPoint)
                {
                    throw new InvalidOperationException();
                }
            }
        }


        /// <summary>
        /// creates a root; a shape with BoundaryCurve set to null 
        /// </summary>
        void GetOrCreateRoot() {
            if (rootShapes.Count() == 0)
            {
                return;
            }

            if (rootShapes.Count() == 1) {
                Shape r = rootShapes.First();
                if (r.BoundaryCurve == null) {
                    root = r;
                    return;
                }
            }
            rootWasCreated = true;
            root = new Shape();
#if TEST_MSAGL
            root.UserData = "root";
#endif
            foreach (Shape rootShape in rootShapes)
            {
                root.AddChild(rootShape);
            }
        }

        void RemoveRoot() {
            if (rootWasCreated)
            {
                foreach (Shape rootShape in rootShapes)
                {
                    rootShape.RemoveParent(root);
                }
            }
        }


#if TEST_MSAGL
        // ReSharper disable UnusedMember.Local
        static void Show(
            IEnumerable<EdgeGeometry> edgeGeometries, IEnumerable<Shape> listOfShapes) {
            // ReSharper restore UnusedMember.Local
            Random r = new Random(1);
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(
                listOfShapes.Select(s => s.BoundaryCurve).Select(
                    c => new DebugCurve(50, 1, DebugCurve.Colors[r.Next(DebugCurve.Colors.Length - 1)], c)).Concat(
                        edgeGeometries.Select(e => new DebugCurve(100, 1, "red", e.Curve))));
        }
#endif


        internal static Dictionary<Shape, Set<Shape>> GetAncestorSetsMap(IEnumerable<Shape> shapes) {
            Dictionary<Shape, Set<Shape>> ancSets = new Dictionary<Shape, Set<Shape>>();
            foreach (Shape child in shapes.Where(child => !ancSets.ContainsKey(child)))
            {
                ancSets[child] = GetAncestorSet(child, ancSets);
            }

            return ancSets;
        }

        static Set<Shape> GetAncestorSet(Shape child, Dictionary<Shape, Set<Shape>> ancSets) {
            Set<Shape> ret = new Set<Shape>(child.Parents);
            foreach (Shape parent in child.Parents) {
                ret += ancSets.TryGetValue(parent, out Set<Shape> grandParents)
                           ? grandParents
                           : ancSets[parent] = GetAncestorSet(parent, ancSets);
            }
            return ret;
        }

        internal static void CreatePortsIfNeeded(IEnumerable<Edge> edges) {
            foreach (Edge edge in edges) {
                if (edge.SourcePort == null) {
                    Edge e = edge;
#if SHARPKIT // Lambdas bind differently in JS
                    edge.SourcePort = ((Func<Edge,RelativeFloatingPort>)(ed => new RelativeFloatingPort(() => ed.Source.BoundaryCurve,
                        () => ed.Source.Center)))(e);
#else
                    edge.SourcePort = new RelativeFloatingPort(() => e.Source.BoundaryCurve, () => e.Source.Center);
#endif
                }
                if (edge.TargetPort == null) {
                    Edge e = edge;
#if SHARPKIT // Lambdas bind differently in JS
                    edge.TargetPort = ((Func<Edge, RelativeFloatingPort>)(ed => new RelativeFloatingPort(() => ed.Target.BoundaryCurve,
                        () => ed.Target.Center)))(e);
#else
                    edge.TargetPort = new RelativeFloatingPort(() => e.Target.BoundaryCurve, () => e.Target.Center);
#endif
                }
            }
        }
        
        /// <summary>
        ///  computes loosePadding for spline routing obstacles from node separation and EdgePadding.
        /// </summary>
        /// <param name="nodeSeparation"></param>
        /// <param name="edgePadding"></param>
        /// <returns></returns>
        public static double ComputeLooseSplinePadding(double nodeSeparation, double edgePadding) {
            Debug.Assert(edgePadding > 0, "require EdgePadding > 0");
            double twicePadding = 2.0 * edgePadding;
            Debug.Assert(nodeSeparation > twicePadding, "require OverlapSeparation > 2*EdgePadding");

            // the 8 divisor is just to guarantee the final postcondition
            double loosePadding = (nodeSeparation - twicePadding) / 8;
            Debug.Assert(loosePadding > 0, "require LoosePadding > 0");
            Debug.Assert(twicePadding + (2 * loosePadding) < nodeSeparation, "EdgePadding too big!");
            return loosePadding;
        }
        

    }
}
