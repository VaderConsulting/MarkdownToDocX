using System;
using System.Collections.Generic;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;

namespace MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms {
    /// <summary>
    /// 
    /// </summary>
    public class MinimumSpanningTreeByPrim {
        readonly BasicGraph<IEdge> graph;
        readonly Func<IEdge, double> weight;
        readonly int root;
        readonly BinaryHeapPriorityQueue q;
        Set<int> treeNodes = [];
        //map of neighbors of the tree to the edges connected them to the tree
        Dictionary<int, IEdge> hedgehog = []; 
        
        /// <summary>
        /// 
        /// </summary>
        public static void Test() {

        }

        static IEnumerable<IntPair> GetEdges(int count) {
            for(int i=0;i<count;i++)
            {
                for (int j = i + 1; j < count; j++) {
                    yield return new IntPair(i,j);
                }
            }
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="weight"></param>
        /// <param name="root">the node we start building the tree</param>
        internal MinimumSpanningTreeByPrim(BasicGraph<IEdge> graph, Func<IEdge, double> weight, int root) {
            this.graph = graph;
            this.weight = weight;
            this.root = root;
            q=new BinaryHeapPriorityQueue(graph.NodeCount);
        }

        bool NodeIsInTree(int i) => treeNodes.Contains(i);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IList<IEdge> GetTreeEdges()
        {
            List<IEdge> ret = new List<IEdge>(graph.NodeCount - 1);
            Init();
            while (ret.Count < graph.NodeCount - 1 && q.Count > 0) //some nodes might have no edges
            {
                AddEdgeToTree(ret);
            }

            return ret;
        }

        void AddEdgeToTree(List<IEdge> ret) {
            int v = q.Dequeue();
            IEdge e = hedgehog[v];
            treeNodes.Insert(v);
            ret.Add(e);
            UpdateOutEdgesOfV(v);
            UpdateInEdgesOfV(v);
        }

        void UpdateOutEdgesOfV(int v) {
            foreach (IEdge outEdge in graph.OutEdges(v)) {
                int u = outEdge.Target;
                if (NodeIsInTree(u))
                {
                    continue;
                }

                if (hedgehog.TryGetValue(u, out IEdge? oldEdge))
                {
                    double oldWeight = weight(oldEdge);
                    double newWeight = weight(outEdge);
                    if (newWeight < oldWeight)
                    {
                        q.DecreasePriority(u, newWeight);
                        hedgehog[u] = outEdge;
                    }
                }
                else
                {
                    q.Enqueue(u, weight(outEdge));
                    hedgehog[u] = outEdge;
                }
            }
        }

        void UpdateInEdgesOfV(int v)
        {
            foreach (IEdge inEdge in graph.InEdges(v))
            {
                int u = inEdge.Source;
                if (NodeIsInTree(u))
                {
                    continue;
                }

                if (hedgehog.TryGetValue(u, out IEdge? oldEdge))
                {
                    double oldWeight = weight(oldEdge);
                    double newWeight = weight(inEdge);
                    if (newWeight < oldWeight)
                    {
                        q.DecreasePriority(u, newWeight);
                        hedgehog[u] = inEdge;
                    }
                }
                else
                {
                    q.Enqueue(u, weight(inEdge));
                    hedgehog[u] = inEdge;
                }
            }
        }

        void Init() {
            treeNodes.Insert(root);

            foreach (IEdge outEdge in graph.OutEdges(root)) {
                double w = weight(outEdge);
                q.Enqueue(outEdge.Target, w);
                hedgehog[outEdge.Target] = outEdge;
            }

            foreach (IEdge inEdge in graph.InEdges(root)) {
                double w = weight(inEdge);
                q.Enqueue(inEdge.Source, w);
                hedgehog[inEdge.Source] = inEdge;
            }
        }
    }
}
