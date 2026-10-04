using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.AGL.Core;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.MDS
{
    /// <summary>
    /// Algorithm for computing the distance between every pair of nodes in a graph.
    /// </summary>
    /// <remarks>
    /// Computes distances between every pair of nodes in a graph.
    /// Distances are symmetric if the graph is undirected.
    /// </remarks>
    /// <param name="graph">A graph.</param>
    /// <param name="directed">Whether shortest paths are directed.</param>
    /// <returns>A square matrix with shortest path distances.</returns>
    public class AllPairsDistances(GeometryGraph graph, bool directed) : AlgorithmBase
    {
        private GeometryGraph graph = graph;

        private bool directed = directed;

        /// <summary>
        /// The resulting distances between every pair of nodes in the graph.
        /// </summary>
        public double[][] Result { get; private set; }

        /// <summary>
        /// Executes the algorithm.
        /// </summary>
        protected override void RunInternal()
        {
            StartListenToLocalProgress(graph.Nodes.Count);
            Result = new double[graph.Nodes.Count][];
            int i = 0;
            foreach (Node source in graph.Nodes)
            {
                SingleSourceDistances distances = new SingleSourceDistances(graph, source, directed);
                distances.Run();

                Result[i] = distances.Result;
                ++i;

                ProgressStep();  // This checks for cancel too.
            }
        }

        /// <summary>
        /// Computes the "stress" of the current layout of the given graph:
        /// 
        ///   stress = sum_{(u,v) in V} D(u,v)^(-2) (d(u,v) - D(u,v))^2
        /// 
        /// where:
        ///   V is the set of nodes
        ///   d(u,v) is the euclidean distance between the centers of nodes u and v
        ///   D(u,v) is the graph-theoretic path length between u and v - scaled by average edge length.
        ///   
        /// The idea of “stress” in graph layout is that nodes that are immediate neighbors should be closer 
        /// together than nodes that are a few hops apart (i.e. that have path length>1).  More generally 
        /// the distance between nodes in the drawing should be proportional to the path length between them.  
        /// The lower the “stress” score of a particular graph layout the better it conforms to this ideal.
        /// 
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static double Stress(GeometryGraph graph)
        {
            ValidateArg.IsNotNull(graph, "graph");
            double stress = 0;
            if (graph.Edges.Count == 0)
            {
                return stress;
            }
            AllPairsDistances apd = new AllPairsDistances(graph, false);
            apd.Run();
            double[][] D = apd.Result;
            double l = graph.Edges.Average(e => e.Length);
            int i = 0;
            foreach (Node u in graph.Nodes)
            {
                int j = 0;
                foreach (Node v in graph.Nodes)
                {
                    if (i != j)
                    {
                        double duv = (u.Center - v.Center).Length;
                        double Duv = l * D[i][j];
                        double d = Duv - duv;
                        stress += d * d / (Duv * Duv);
                    }
                    ++j;
                }
                ++i;
            }
            return stress;
        }
    }
}
