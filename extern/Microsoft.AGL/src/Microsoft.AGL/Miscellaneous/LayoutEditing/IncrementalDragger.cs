using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Layout.LargeGraphLayout;
using Microsoft.AGL.Miscellaneous.LayoutEditing;
using Microsoft.AGL.Routing;
using GeomNode = Microsoft.AGL.Core.Layout.Node;

namespace Microsoft.AGL.Miscellaneous.LayoutEditing
{
    /// <summary>
    /// 
    /// </summary>
    public class IncrementalDragger {
        GeometryGraph graph { get; set; }
        readonly double nodeSeparation;
        readonly LayoutAlgorithmSettings layoutSettings;
        List<BumperPusher> listOfPushers = [];
        readonly GeomNode[] pushingNodesArray;
        /// <summary>
        /// it is smaller graph that needs to be refreshed by the viewer
        /// </summary>
        public GeometryGraph ChangedGraph;
        Dictionary<EdgeGeometry,LabelFixture> labelFixtures=[];
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pushingNodes">the nodes are already at the correct positions</param>
        /// <param name="graph"></param>
        /// <param name="layoutSettings"></param>
        public IncrementalDragger(IEnumerable<GeomNode> pushingNodes, GeometryGraph graph, LayoutAlgorithmSettings layoutSettings) {
            this.graph = graph;
            nodeSeparation = layoutSettings.NodeSeparation;
            this.layoutSettings = layoutSettings;
            pushingNodesArray = pushingNodes as GeomNode[] ?? pushingNodes.ToArray();
            Debug.Assert(pushingNodesArray.All(n => DefaultClusterParent(n) == null) ||
                          (new Set<GeomNode>(pushingNodesArray.Select(n => n.ClusterParents.First()))).Count == 1,
                                    "dragged nodes have to belong to the same cluster");
            InitBumperPushers();
        }

        void InitBumperPushers() {
            if (pushingNodesArray.Length == 0) return;
            Cluster cluster = DefaultClusterParent(pushingNodesArray[0]);
            if (cluster == null)
                listOfPushers.Add(new BumperPusher(graph.Nodes, nodeSeparation, pushingNodesArray));
            else {
                listOfPushers.Add( new BumperPusher(cluster.Nodes.Concat(cluster.Clusters), nodeSeparation,
                                                             pushingNodesArray));
                do {
                    Cluster pushingCluster = cluster;
                    cluster = DefaultClusterParent(cluster);
                    if (cluster == null) break;
                    listOfPushers.Add(new BumperPusher(cluster.Nodes.Concat(cluster.Clusters), nodeSeparation,
                                                       new[] {pushingCluster}));

                } while (true);
            }
        }


        static Cluster DefaultClusterParent(GeomNode n) => n.ClusterParents.FirstOrDefault();

        void RunPushers() {
            for (int i = 0; i < listOfPushers.Count;i++ ) {
                BumperPusher bumperPusher = listOfPushers[i];
                bumperPusher.PushNodes();
                Cluster cluster = DefaultClusterParent(bumperPusher.FirstPushingNode());
                if (cluster == null || cluster==graph.RootCluster) break;
                Rectangle box = cluster.BoundaryCurve.BoundingBox;
                cluster.CalculateBoundsFromChildren(layoutSettings.ClusterMargin);
                Debug.Assert(cluster.Nodes.All(n => cluster.BoundingBox.Contains(n.BoundingBox)));
                Rectangle newBox = cluster.BoundaryCurve.BoundingBox;
                if (newBox == box) {
                    break;
                }
                listOfPushers[i + 1].UpdateRTreeByChangedNodeBox(cluster, box);
            } 
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="delta"></param>
        public void Drag(Point delta) {
            if(delta.Length>0)
                foreach (GeomNode n in pushingNodesArray) {
                    n.Center += delta;
                    if (n is Cluster cl)
                        cl.DeepContentsTranslation(delta, true);
                }

            RunPushers();
            RouteChangedEdges();
        }

        void RouteChangedEdges() {
            ChangedGraph = GetChangedFlatGraph();
            GeometryGraph changedClusteredGraph = LgInteractor.CreateClusteredSubgraphFromFlatGraph(ChangedGraph, graph);


            InitLabelFixtures(changedClusteredGraph);
            SplineRouter router = new SplineRouter(changedClusteredGraph, layoutSettings.EdgeRoutingSettings.Padding,
                                          layoutSettings.EdgeRoutingSettings.PolylinePadding,
                                          layoutSettings.EdgeRoutingSettings.ConeAngle,
                                          layoutSettings.EdgeRoutingSettings.BundlingSettings) {
                                              ContinueOnOverlaps
                                                  = true
                                          };

            router.Run();
            PositionLabels(changedClusteredGraph);

        }

        void PositionLabels(GeometryGraph changedClusteredGraph) {
            foreach (Edge edge in changedClusteredGraph.Edges)
                PositionEdge(edge);
        }

        void PositionEdge(Edge edge) {
            if (!labelFixtures.TryGetValue(edge.EdgeGeometry, out LabelFixture lf)) return;
            ICurve curve = edge.Curve;
            double lenAtLabelAttachment = curve.Length*lf.RelativeLengthOnCurve;
            double par = curve.GetParameterAtLength(lenAtLabelAttachment);
            Point tang = curve.Derivative(par);
            Point norm = (lf.RightSide ? tang.Rotate90Cw() : tang.Rotate90Ccw()).Normalize()*lf.NormalLength;
            edge.Label.Center = curve[par] + norm;           
        }

        void InitLabelFixtures(GeometryGraph changedClusteredGraph) {
            foreach (Edge edge in changedClusteredGraph.Edges)
                InitLabelFixture(edge);
        }

        void InitLabelFixture(Edge edge) {
            if (edge.Label == null) return;
            if (labelFixtures.ContainsKey(edge.EdgeGeometry)) return;

            double attachmentPar = edge.Curve.ClosestParameter(edge.Label.Center);

            ICurve curve = edge.Curve;
            Point tang = curve.Derivative(attachmentPar);
            Point normal = tang.Rotate90Cw();
            Point fromCurveToLabel = edge.Label.Center - curve[attachmentPar];
            LabelFixture fixture = new LabelFixture() {
                RelativeLengthOnCurve = curve.LengthPartial(0, attachmentPar)/curve.Length,
                NormalLength = fromCurveToLabel.Length,
                RightSide = fromCurveToLabel*normal>0
            };

            labelFixtures[edge.EdgeGeometry] = fixture;
        }


        GeometryGraph GetChangedFlatGraph() {
            Set<GeomNode> changedNodes = GetChangedNodes();
            List<Edge> changedEdges = GetChangedEdges(changedNodes);
            foreach (Edge e in changedEdges) {
                changedNodes.Insert(e.Source);
                changedNodes.Insert(e.Target);
            }

            GeometryGraph changedGraph = new GeometryGraph {
                Nodes = new SimpleNodeCollection(changedNodes),
                Edges = new SimpleEdgeCollection(changedEdges)
            };
            return changedGraph;
        }

        List<Edge> GetChangedEdges(Set<Node> changedNodes) {
            List<Edge> list = new List<Edge>();
            Rectangle box = Rectangle.CreateAnEmptyBox();
            foreach(GeomNode node in changedNodes)
                box.Add(node.BoundaryCurve.BoundingBox);


            Polyline boxPoly = box.Perimeter();

            foreach (Edge e in graph.Edges)
                if (EdgeNeedsRouting(ref box, e, boxPoly, changedNodes))
                    list.Add(e);
            return list;
        }

        bool EdgeNeedsRouting(ref Rectangle box, Edge edge, Polyline boxPolyline, Set<Node> changedNodes) {
            if (edge.Curve == null)
                return true;
            if (changedNodes.Contains(edge.Source) || changedNodes.Contains(edge.Target))
                return true;
            if (edge.Source.BoundaryCurve.BoundingBox.Intersects(box) ||
                edge.Target.BoundaryCurve.BoundingBox.Intersects(box))
                return true;

            if (!edge.BoundingBox.Intersects(box))
                return false;

            return Curve.CurveCurveIntersectionOne(boxPolyline, edge.Curve, false) != null;
        }

        Set<Node> GetChangedNodes() => new Set<Node>(listOfPushers.SelectMany(p => p.FixedNodes));
    }
}