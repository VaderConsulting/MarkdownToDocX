using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    internal class ShortestPartRouterForLg {
        readonly Node source;
        readonly Node target;
        GenericBinaryHeapPriorityQueue<Node> queue = new GenericBinaryHeapPriorityQueue<Node>();
        Dictionary<Node, Tuple<Edge,double>> prev = [];
        Point pathDirection;
        double alpha = 0.5;
        double costToTarget;
        Edge bestEdgeIntoTarget;
        bool ignoreInteresingEdgesFunc;
        Func<Node,LgNodeInfo> geomNodeToLgNode;

        internal Func<Node,Node, bool> EdgeIsInterestingFunc { get; set; }
        internal bool ConsiderOnlyInterestingEdges { get; set; }


        public ShortestPartRouterForLg(Node source, Node target, Func<Node, LgNodeInfo> geomNodeToLgNode) {
            this.source = source;
            this.target = target;
            this.geomNodeToLgNode = geomNodeToLgNode;
            queue.Enqueue(source, 0);
            pathDirection = target.Center - source.Center;
            costToTarget = double.PositiveInfinity;
            EdgeIsInterestingFunc = MonotonicityFunc;
        }

        bool MonotonicityFunc(Node a, Node b) {
            Point edgeVector = a.Center - b.Center;
            return edgeVector*pathDirection >= 0;
        }


        public Edge[] Run() {
            ignoreInteresingEdgesFunc = false;
            Edge[] ret =SearchForPath().ToArray();
            if (ret.Length>0)
                return ret;

            if (ConsiderOnlyInterestingEdges)
                    return null;
            ignoreInteresingEdgesFunc = true;
            Cleanup();
            Edge[] p = SearchForPath().ToArray();
            return p;
        }

    
        void Cleanup() {
            costToTarget = double.PositiveInfinity;
            bestEdgeIntoTarget = null;
            prev.Clear();
            queue = new GenericBinaryHeapPriorityQueue<Node>();
            queue.Enqueue(source, 0);
        }

        IEnumerable<Edge> SearchForPath() {
            while (queue.Count > 0 ) {
                Node node = queue.Dequeue(out double costPlus);
                if (costPlus >= costToTarget)
                    break;
                double cost = node == source ? 0 : prev[node].Item2;
                ProcessVertex(node, cost);
            }
            IEnumerable<Edge> ret = RecoverPath();
            return ret;
        }

        IEnumerable<Edge> RecoverPath() {
            if (bestEdgeIntoTarget != null) {
                Node currentNode = target;
                Edge currentEdge = bestEdgeIntoTarget;
                do {
                    yield return currentEdge;
                    currentNode = GetOtherVertex(currentEdge, currentNode);
                    if (currentNode == source)
                        break;
                    currentEdge = prev[currentNode].Item1;
                } while (true);
            }
        }

        Node GetOtherVertex(Edge currentEdge, Node currentNode) => currentEdge.Source == currentNode ? currentEdge.Target : currentEdge.Source;

        void ProcessVertex(Node node, double cost) {
            foreach (Edge outEdge in node.OutEdges)
                ProcessOutEdge(outEdge, cost);

            foreach (Edge outEdge in node.InEdges)
                ProcessInEdge(outEdge, cost);
        }

        void ProcessInEdge(Edge inEdge, double cost) {
            Point edgeVector = inEdge.Source.Center - inEdge.Target.Center;
            if (!ignoreInteresingEdgesFunc && EdgeIsInterestingFunc!=null &&  !EdgeIsInterestingFunc(inEdge.Target,inEdge.Source)) return;
            double costPlus = CalculateCostPlus(inEdge, edgeVector);
            double totalCostToEdgeEnd = cost + costPlus;
            if (inEdge.Source == target) {
                if (totalCostToEdgeEnd < costToTarget) {
                    costToTarget = totalCostToEdgeEnd;
                    bestEdgeIntoTarget = inEdge;
                }
            } else {
                double h = AStarH(inEdge.Source);
                if (totalCostToEdgeEnd + h >= costToTarget)
                    return;
                if (prev.TryGetValue(inEdge.Source, out Tuple<Edge, double>? storedPrev))
                {
                    if (storedPrev.Item2 > totalCostToEdgeEnd)
                    {
                        prev[inEdge.Source] = new Tuple<Edge, double>(inEdge, totalCostToEdgeEnd);
                        queue.Enqueue(inEdge.Source, totalCostToEdgeEnd + h);
                    }
                }
                else
                {
                    prev[inEdge.Source] = new Tuple<Edge, double>(inEdge, totalCostToEdgeEnd);
                    queue.Enqueue(inEdge.Source, totalCostToEdgeEnd + h);
                }
            }
        }

        void ProcessOutEdge(Edge outEdge, double cost) {
            Point edgeVector = outEdge.Target.Center - outEdge.Source.Center;
            if (!ignoreInteresingEdgesFunc && EdgeIsInterestingFunc != null && !EdgeIsInterestingFunc(outEdge.Source, outEdge.Target)) return; 
            double costPlus = CalculateCostPlus(outEdge, edgeVector);
            double totalCostToEdgeEnd = cost + costPlus;
            if (outEdge.Target == target) {
                if (totalCostToEdgeEnd < costToTarget) {
                    costToTarget = totalCostToEdgeEnd;
                    bestEdgeIntoTarget = outEdge;
                }
            }
            else {
                double h = AStarH(outEdge.Target);
                if (totalCostToEdgeEnd + h >= costToTarget)
                    return;
                if (prev.TryGetValue(outEdge.Target, out Tuple<Edge, double>? storedPrev))
                {
                    if (storedPrev.Item2 > totalCostToEdgeEnd)
                    {
                        prev[outEdge.Target] = new Tuple<Edge, double>(outEdge, totalCostToEdgeEnd);
                        queue.Enqueue(outEdge.Target, totalCostToEdgeEnd + h);
                    }
                }
                else
                {
                    prev[outEdge.Target] = new Tuple<Edge, double>(outEdge, totalCostToEdgeEnd);
                    queue.Enqueue(outEdge.Target, totalCostToEdgeEnd + h);
                }
            }
        }

        double AStarH(Node node) => (node.Center - target.Center).Length * (1 - alpha);

        double CalculateCostPlus(Edge edge, Point edgeVector) => edgeVector.Length * (1 - alpha / ZoomLevel(edge));//diminishing the edge length for important edges

        double ZoomLevel(Edge edge) {
            LgNodeInfo src = geomNodeToLgNode(edge.Source);
            LgNodeInfo trg = geomNodeToLgNode(edge.Target);
            return Math.Max(src.ZoomLevel, trg.ZoomLevel);
        }
    }
}