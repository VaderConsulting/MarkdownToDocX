using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms.CycleRemoval;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing {
    internal class MultiEdgeRouter {
        readonly List<EdgeGeometry[]> multiEdgeGeometries;
        readonly InteractiveEdgeRouter interactiveEdgeRouter;
        readonly BundlingSettings bundlingSettings;
        readonly Func<EdgeGeometry, List<Shape>> transparentShapeSetter;
        readonly RectangleNode<ICurve> nodeTree;


        internal MultiEdgeRouter(List<Edge[]> multiEdgeGeoms, InteractiveEdgeRouter interactiveEdgeRouter, IEnumerable<ICurve> nodeBoundaryCurves, BundlingSettings bundlingSettings, Func<EdgeGeometry, List<Shape>> transparentShapeSetter) {
            multiEdgeGeometries = [.. multiEdgeGeoms.Select(l => l.Select(e => e.EdgeGeometry).ToArray())];
            
            this.interactiveEdgeRouter = interactiveEdgeRouter;
            this.bundlingSettings = bundlingSettings;
            this.transparentShapeSetter = transparentShapeSetter;
            nodeTree = RectangleNode<ICurve>.CreateRectangleNodeOnData(nodeBoundaryCurves, c => c.BoundingBox);
        }

        internal void Run() {
            foreach (GeometryGraph graph in GetGeometryGraphs()) {
                BundleRouter br = new BundleRouter(graph, new SdShortestPath(transparentShapeSetter, null, null),
                    interactiveEdgeRouter.VisibilityGraph, bundlingSettings, interactiveEdgeRouter.LoosePadding, interactiveEdgeRouter.TightHierarchy,
                    interactiveEdgeRouter.LooseHierarchy, null, null, null);

                br.Run();
            }
        }

        IEnumerable<GeometryGraph> GetGeometryGraphs() {
            foreach (PreGraph preGraph in GetIndependantPreGraphs())
            {
                yield return CreateGeometryGraph(preGraph);
            }
        }

        GeometryGraph CreateGeometryGraph(PreGraph preGraph) {
            GeometryGraph graph = new GeometryGraph();
            Dictionary<ICurve, Node> nodeDictionary = [];
            foreach (ICurve curve in preGraph.nodeBoundaries) {
                Node node = new Node(curve);
                nodeDictionary[curve] = node;
                graph.Nodes.Add(node);
            }
            foreach (EdgeGeometry eg in preGraph.edgeGeometries)
            {
                AddEdgeGeometryToGraph(eg, graph, nodeDictionary);
            }

            return graph;
        }

        void AddEdgeGeometryToGraph(EdgeGeometry eg, GeometryGraph graph, Dictionary<ICurve, Node> nodeDictionary) {
            Node sourceNode = GetOrCreateNode(eg.SourcePort, nodeDictionary);
            Node targetNode = GetOrCreateNode(eg.TargetPort, nodeDictionary);
            Edge edge = new Edge(sourceNode, targetNode) { EdgeGeometry = eg };
            graph.Edges.Add(edge);
        }

        private Node GetOrCreateNode(Port port, Dictionary<ICurve, Node> nodeDictionary) {
            ICurve curve = GetPortCurve(port);
            if (!nodeDictionary.TryGetValue(curve, out Node? node))
            {
                nodeDictionary[curve] = node = new Node(curve);
            }

            return node;
        }

        private ICurve GetPortCurve(Port port) {
            ICurve? curve = nodeTree.FirstHitNode(port.Location,
                (point, c) => Curve.PointRelativeToCurveLocation(point, c) != PointLocation.Outside ? HitTestBehavior.Stop : HitTestBehavior.Continue).UserData;
            return curve;
        }
        /// <summary>
        /// creates a set of pregraphs suitable for bundle routing
        /// </summary>
        /// <returns></returns>
        IEnumerable<PreGraph> GetIndependantPreGraphs() {
            List<PreGraph> preGraphs = CreateInitialPregraphs();
            do {
                int count = preGraphs.Count;
                UniteConnectedPreGraphs(ref preGraphs);
                if (count <= preGraphs.Count)
                {
                    break;
                }
            } while (true);
            return preGraphs;
        }

        void UniteConnectedPreGraphs(ref List<PreGraph> preGraphs) {
            BasicGraph<IntPair> intersectionGraph = GetIntersectionGraphOfPreGraphs(preGraphs);
            if (intersectionGraph == null)
            {
                return;
            }

            IEnumerable<IEnumerable<int>> connectedComponents = ConnectedComponentCalculator<IntPair>.GetComponents(intersectionGraph);
            List<PreGraph> newPreGraphList = [];
            foreach (IEnumerable<int> component in connectedComponents) {
                PreGraph preGraph = null;
                foreach (int i in component) {
                    if (preGraph == null) {
                        preGraph = preGraphs[i];
                        newPreGraphList.Add(preGraph);
                    }
                    else
                    {
                        preGraph.AddGraph(preGraphs[i]);
                    }
                }
            }
            preGraphs = newPreGraphList;
            foreach (PreGraph pg in preGraphs)
            {
                AddIntersectingNodes(pg);
            }
        }

        private void AddIntersectingNodes(PreGraph pg) {
            Rectangle rect = pg.boundingBox;
            foreach (ICurve curve in nodeTree.GetNodeItemsIntersectingRectangle(rect))
            {
                pg.AddNodeBoundary(curve);
            }
        }

        static BasicGraph<IntPair> GetIntersectionGraphOfPreGraphs(List<PreGraph> preGraphs) {
            IEnumerable<IntPair> intersectingPairs = EnumeratePairsOfIntersectedPreGraphs(preGraphs);
            if (intersectingPairs.Any())
            {
                return new BasicGraph<IntPair>(intersectingPairs, preGraphs.Count);
            }

            return null;
        }

        static IEnumerable<IntPair> EnumeratePairsOfIntersectedPreGraphs(List<PreGraph> preGraphs) {
            RectangleNode<int> rn = RectangleNode<int>.CreateRectangleNodeOnData(Enumerable.Range(0, preGraphs.Count), i => preGraphs[i].boundingBox);
            List<IntPair> list = [];
            RectangleNodeUtils.CrossRectangleNodes<int>(rn, rn, (a, b) => list.Add(new IntPair(a, b)));
            return list;
        }

        List<PreGraph> CreateInitialPregraphs() => new List<PreGraph>(multiEdgeGeometries.Select(CreatePregraphFromSetOfEdgeGeometries));

        private PreGraph CreatePregraphFromSetOfEdgeGeometries(EdgeGeometry[] egs) {
            Set<ICurve> nodeBoundaries = [];
            EdgeGeometry eg = egs[0];
            ICurve c = GetPortCurve(eg.SourcePort);
            Rectangle rect = c.BoundingBox;
            nodeBoundaries.Insert(c);
            nodeBoundaries.Insert(eg.TargetPort.Curve);
            rect.Add(eg.TargetPort.Curve.BoundingBox);
            IEnumerable<ICurve> overlapped = nodeTree.GetNodeItemsIntersectingRectangle(rect);
            foreach (ICurve nodeBoundary in overlapped)
            {
                nodeBoundaries.Insert(nodeBoundary);
            }

            return new PreGraph(egs, nodeBoundaries);
        }
    }
}
