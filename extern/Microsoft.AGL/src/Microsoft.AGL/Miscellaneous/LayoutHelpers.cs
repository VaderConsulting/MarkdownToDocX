using Microsoft.AGL.Core.Geometry.OverlapRemoval;
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
using Microsoft.AGL.Layout.Incremental;
using Microsoft.AGL.Layout.Initial;
using Microsoft.AGL.Layout.LargeGraphLayout;
using Microsoft.AGL.Layout.Layered;
using Microsoft.AGL.Layout.MDS;
using Microsoft.AGL.Routing;
using Microsoft.AGL.Routing.Rectilinear;
using Microsoft.AGL.Miscellaneous.Ranking;
using Microsoft.AGL.Routing.Spline.Bundling;

namespace Microsoft.AGL.Miscellaneous
{
    /// <summary>
    /// A set of helper methods for executing a layout.
    /// These exist for compatibility with previous consumers of MSAGL,
    /// but the new APIs should be used for new code.
    /// </summary>
    public static class LayoutHelpers
    {
#if SILVERLIGHT
        /// <summary>
        /// Calculates the graph layout
        /// </summary>
        /// <exception cref="CancelException">Thrown when the layout is canceled.</exception>
#else
        /// <summary>
        /// Calculates the graph layout
        /// </summary>
        /// <exception cref="System.OperationCanceledException">Thrown when the layout is canceled.</exception>
#endif
        public static void CalculateLayout(GeometryGraph geometryGraph, LayoutAlgorithmSettings settings, CancelToken cancelToken) {
            Console.WriteLine("starting CalculateLayout");
            if (settings is RankingLayoutSettings) {
                RankingLayoutSettings rankingLayoutSettings = settings as RankingLayoutSettings;
                RankingLayout rankingLayout = new RankingLayout(rankingLayoutSettings, geometryGraph);
                rankingLayout.Run(cancelToken);
                RouteAndLabelEdges(geometryGraph, settings, geometryGraph.Edges);
            }
            else if (settings is MdsLayoutSettings) {
                MdsLayoutSettings mdsLayoutSettings = settings as MdsLayoutSettings;
                MdsGraphLayout mdsLayout = new MdsGraphLayout(mdsLayoutSettings, geometryGraph);
                mdsLayout.Run(cancelToken);
                if (settings.EdgeRoutingSettings.EdgeRoutingMode != EdgeRoutingMode.None)
                {
                    RouteAndLabelEdges(geometryGraph, settings, geometryGraph.Edges);
                }
            }
            else if (settings is FastIncrementalLayoutSettings) {
                FastIncrementalLayoutSettings incrementalSettings = settings as FastIncrementalLayoutSettings;
                incrementalSettings.AvoidOverlaps = true;
                InitialLayout initialLayout = new InitialLayout(geometryGraph, incrementalSettings);
                initialLayout.Run(cancelToken);
                if (settings.EdgeRoutingSettings.EdgeRoutingMode != EdgeRoutingMode.None)
                {
                    RouteAndLabelEdges(geometryGraph, settings, geometryGraph.Edges);
                }
                //incrementalSettings.IncrementalRun(geometryGraph);
            }
            else {
                if (settings is SugiyamaLayoutSettings sugiyamaLayoutSettings)
                {
                    ProcessSugiamaLayout(geometryGraph, sugiyamaLayoutSettings, cancelToken);
                }
                else
                {
                    Debug.Assert(settings is LgLayoutSettings);
                    LayoutLargeGraphWithLayers(geometryGraph, settings, cancelToken);
                }
            }
        }

        /// <summary>
        /// calculates all data necessery for large graph browsing
        /// </summary>
        /// <param name="geometryGraph"></param>
        /// <param name="settings"></param>
        /// <param name="cancelToken"></param>
        public static void LayoutLargeGraphWithLayers(GeometryGraph geometryGraph, LayoutAlgorithmSettings settings, CancelToken cancelToken) {
            LgLayoutSettings largeGraphLayoutSettings = (LgLayoutSettings) settings;
            LgInteractor largeGraphLayout = new LgInteractor(geometryGraph, largeGraphLayoutSettings, cancelToken);
            largeGraphLayoutSettings.Interactor = largeGraphLayout;
            largeGraphLayout.Run();
        }

        static void ProcessSugiamaLayout(GeometryGraph geometryGraph, SugiyamaLayoutSettings sugiyamaLayoutSettings, CancelToken cancelToken) {
            bool transformIsNotIdentity = HandleTransformIsNotIdentity(geometryGraph, sugiyamaLayoutSettings, out PlaneTransformation originalTransform);

            if (geometryGraph.RootCluster.Clusters.Any()) {
                PrepareGraphForInitialLayoutByCluster(geometryGraph, sugiyamaLayoutSettings);
                InitialLayoutByCluster initialBc = new InitialLayoutByCluster(geometryGraph, a => sugiyamaLayoutSettings);
                initialBc.Run(cancelToken);
                //route the rest of the edges, those between the clusters
                RouteAndLabelEdges(geometryGraph, sugiyamaLayoutSettings,
                    geometryGraph.Edges.Where(e => e.Curve == null).ToArray());
            }
            else
            {
                geometryGraph.AlgorithmData = SugiyamaLayoutSettings.CalculateLayout(geometryGraph,
                    sugiyamaLayoutSettings, cancelToken);
            }

            if (transformIsNotIdentity)
            {
                sugiyamaLayoutSettings.Transformation = originalTransform;
            }

            PostRunTransform(geometryGraph, sugiyamaLayoutSettings);
        }

        static bool HandleTransformIsNotIdentity(GeometryGraph geometryGraph, SugiyamaLayoutSettings sugiyamaLayoutSettings,
            out PlaneTransformation originalTransform) {
            bool transformIsNotIdentity = !sugiyamaLayoutSettings.Transformation.IsIdentity;
            originalTransform = sugiyamaLayoutSettings.Transformation;
            if (transformIsNotIdentity) {
                PlaneTransformation m = sugiyamaLayoutSettings.Transformation.Inverse;
                foreach (Node n in geometryGraph.Nodes)
                {
                    n.Transform(m);
                }
                //calculate new label widths and heights
                foreach (Edge e in geometryGraph.Edges) {
                    if (e.Label != null) {
                        e.OriginalLabelWidth = e.Label.Width;
                        e.OriginalLabelHeight = e.Label.Height;
                        Rectangle r = new Rectangle(m*new Point(0, 0), m*new Point(e.Label.Width, e.Label.Height));
                        e.Label.Width = r.Width;
                        e.Label.Height = r.Height;
                    }
                }
            }
            sugiyamaLayoutSettings.Transformation = PlaneTransformation.UnitTransformation; //restore it later
            return transformIsNotIdentity;
        }

        static void PrepareGraphForInitialLayoutByCluster(GeometryGraph geometryGraph,
            SugiyamaLayoutSettings sugiyamaLayoutSettings) {
            foreach (Cluster cluster in geometryGraph.RootCluster.AllClustersDepthFirst()) {
                if (cluster.RectangularBoundary == null) {}
                cluster.RectangularBoundary = new RectangularClusterBoundary() {TopMargin = 10};

                if (cluster.BoundaryCurve == null) {
                    cluster.BoundaryCurve = new RoundedRect(new Rectangle(0, 0, 10, 10), 3, 3);
                }
            }

            foreach (Edge edge in geometryGraph.Edges) {
                edge.Curve = null;
                if (edge.SourcePort == null) {
                    Edge e = edge;
#if SHARPKIT // Lambdas bind differently in JS
                    edge.SourcePort = ((Func<Edge,RelativeFloatingPort>)(ed => new RelativeFloatingPort(() => ed.Source.BoundaryCurve,
                        () => ed.Source.Center)))(e);
#else
                    edge.SourcePort = new RelativeFloatingPort(() => e.Source.BoundaryCurve,
                        () => e.Source.Center);
#endif
                }
                if (edge.TargetPort == null) {
                    Edge e = edge;
#if SHARPKIT // Lambdas bind differently in JS
                    edge.TargetPort = ((Func<Edge, RelativeFloatingPort>)(ed => new RelativeFloatingPort(() => ed.Target.BoundaryCurve,
                        () => ed.Target.Center)))(e);
#else
                    edge.TargetPort = new RelativeFloatingPort(() => e.Target.BoundaryCurve,
                        () => e.Target.Center);
#endif
                }
            }
            if (sugiyamaLayoutSettings.FallbackLayoutSettings == null)
            {
                sugiyamaLayoutSettings.FallbackLayoutSettings = new FastIncrementalLayoutSettings() {
                    AvoidOverlaps = true
                };
            }

            AddOrphanNodesToRootCluster(geometryGraph);
        }

        static void AddOrphanNodesToRootCluster(GeometryGraph geometryGraph) {
            Set<Node> clusterNodeSet = new Set<Node>();
            foreach(Cluster cl in geometryGraph.RootCluster.AllClustersDepthFirst())
            {
                clusterNodeSet.InsertRange(cl.Nodes);
            }

            foreach (Node node in geometryGraph.Nodes) {
                if (clusterNodeSet.Contains(node))
                {
                    continue;
                }

                geometryGraph.RootCluster.AddNode(node);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="geometryGraph"></param>
        /// <param name="layoutSettings"></param>
        /// <param name="edgesToRoute"></param>
        public static void RouteAndLabelEdges(GeometryGraph geometryGraph, LayoutAlgorithmSettings layoutSettings, IEnumerable<Edge> edgesToRoute) {
            //todo: what about parent edges!!!!
            Edge[] filteredEdgesToRoute =
                edgesToRoute.Where(e => ! e.UnderCollapsedCluster()).ToArray();
            EdgeRoutingSettings ers = layoutSettings.EdgeRoutingSettings;
            if (ers.EdgeRoutingMode == EdgeRoutingMode.Rectilinear ||
                ers.EdgeRoutingMode == EdgeRoutingMode.RectilinearToCenter) {
                RectilinearInteractiveEditor.CreatePortsAndRouteEdges(
                    layoutSettings.NodeSeparation/3,
                    layoutSettings.NodeSeparation/3,
                    geometryGraph.Nodes,
                    edgesToRoute,
                    ers.EdgeRoutingMode,
                    true,
                    ers.UseObstacleRectangles,
                    ers.BendPenalty);
            } else if (ers.EdgeRoutingMode == EdgeRoutingMode.Spline || ers.EdgeRoutingMode==EdgeRoutingMode.SugiyamaSplines) {
                new SplineRouter(geometryGraph, filteredEdgesToRoute, ers.Padding, ers.PolylinePadding, ers.ConeAngle, null)
                {
                    ContinueOnOverlaps = true, KeepOriginalSpline = ers.KeepOriginalSpline
                }.Run();
            } else if (ers.EdgeRoutingMode == EdgeRoutingMode.SplineBundling) {
                BundlingSettings edgeBundlingSettings = ers.BundlingSettings ?? new BundlingSettings();
                SplineRouter bundleRouter = new SplineRouter(geometryGraph, filteredEdgesToRoute, ers.Padding, ers.PolylinePadding, ers.ConeAngle,
                                                    edgeBundlingSettings) {
                                                        KeepOriginalSpline = ers.KeepOriginalSpline
                                                    };
                bundleRouter.Run();
                if (bundleRouter.OverlapsDetected)
                {
                    new SplineRouter(geometryGraph, filteredEdgesToRoute, ers.Padding, ers.PolylinePadding, ers.ConeAngle, null)
                    {
                        ContinueOnOverlaps = true,
                        KeepOriginalSpline = ers.KeepOriginalSpline
                    }.Run();
                }                
            } else if (ers.EdgeRoutingMode == EdgeRoutingMode.StraightLine) {
                StraightLineEdges router = new StraightLineEdges(filteredEdgesToRoute, ers.Padding);
                router.Run();
            }
            EdgeLabelPlacement elb = new EdgeLabelPlacement(geometryGraph.Nodes, filteredEdgesToRoute);
            elb.Run();
            geometryGraph.UpdateBoundingBox();
        }

        

        /// <summary>
        /// adaptes to the node boundary curve change
        /// </summary>
        public static void IncrementalLayout(GeometryGraph geometryGraph, Node node, SugiyamaLayoutSettings settings)
        {
            if(settings==null)
            {
                return;
            }

            if (geometryGraph.AlgorithmData is LayeredLayoutEngine engine)
            {
                engine.IncrementalRun(node);
                PostRunTransform(geometryGraph, settings);
            }
        }

        static void PostRunTransform(GeometryGraph geometryGraph, SugiyamaLayoutSettings settings) {
            bool transform = !settings.Transformation.IsIdentity;
            if (transform) {
                foreach (Node n in geometryGraph.Nodes)
                {
                    n.Transform(settings.Transformation);
                }

                foreach (Cluster n in geometryGraph.RootCluster.Clusters) {
                    n.Transform(settings.Transformation);
                    n.RectangularBoundary.Rect = n.BoundaryCurve.BoundingBox;
                }

                //restore labels widths and heights
                foreach (Edge e in geometryGraph.Edges) {
                    if (e.Label != null) {
                        e.Label.Width = e.OriginalLabelWidth;
                        e.Label.Height = e.OriginalLabelHeight;
                    }
                }

                TransformCurves(geometryGraph, settings);
            }

            geometryGraph.UpdateBoundingBox();
        }

        static void TransformCurves(GeometryGraph geometryGraph, SugiyamaLayoutSettings settings)
        {
            PlaneTransformation transformation = settings.Transformation;
            geometryGraph.BoundingBox = new Rectangle(transformation * geometryGraph.LeftBottom, transformation * geometryGraph.RightTop);
            foreach (Edge e in geometryGraph.Edges)
            {
                if (e.Label != null)
                {
                    e.Label.Center = transformation * e.Label.Center;
                }

                if (e.Curve != null)
                {
                    e.Curve = e.Curve.Transform(transformation);
                    EdgeGeometry eg = e.EdgeGeometry;
                    if (eg.SourceArrowhead != null)
                    {
                        eg.SourceArrowhead.TipPosition = transformation * eg.SourceArrowhead.TipPosition;
                    }

                    if (eg.TargetArrowhead != null)
                    {
                        eg.TargetArrowhead.TipPosition = transformation * eg.TargetArrowhead.TipPosition;
                    }

                    TransformUnderlyingPolyline(e, settings);
                }
            }
        }

        static void TransformUnderlyingPolyline(Edge e, SugiyamaLayoutSettings settings)
        {
            if (e.UnderlyingPolyline != null)
            {
                for (Site s = e.UnderlyingPolyline.HeadSite; s != null; s = s.Next)
                {
                    s.Point = settings.Transformation*s.Point;
                }
            }
        }
    }
}
