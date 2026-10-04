using System.Diagnostics;
using System.Linq;

using Microsoft.AGL.Core;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.MDS
{
    /// <summary>
    /// Initial layout using PivotMDS method for FastIncrementalLayout
    /// </summary>
    public class PivotMDS : AlgorithmBase
    {
        class PivotMDSNodeWrap
        {
            internal Node node;
            internal PivotMDSNodeWrap(Node node) => this.node = node;
        }

        private GeometryGraph graph;

        /// <summary>
        /// scales the final layout by the specified factor
        /// </summary>
        public double Scale { get; set; }

        /// <summary>
        /// Layout graph by the PivotMds method.  Uses spectral techniques to obtain a layout in
        /// O(n^2) time.
        /// </summary>
        /// <param name="graph"></param>
        public PivotMDS(GeometryGraph graph)
        {
            this.graph = graph;
            Scale = 1;
        }

        /// <summary>
        /// Executes the algorithm.
        /// </summary>
        /// <summary>
        /// Executes the actual algorithm.
        /// </summary>
        protected override void RunInternal()
        {
            GeometryGraph g = new GeometryGraph();
            foreach (Node v in graph.Nodes)
            {
                Debug.Assert(!(v is Cluster));
                Node u = new Node(v.BoundaryCurve.Clone())
                {
                    UserData = v
                };
                v.AlgorithmData = new PivotMDSNodeWrap(u);
                g.Nodes.Add(u);
            }
            double avgLength = 0;
            foreach (Edge e in graph.Edges)
            {
                avgLength += e.Length;
                if (e.Source is Cluster || e.Target is Cluster) continue;
                PivotMDSNodeWrap u = e.Source.AlgorithmData as PivotMDSNodeWrap;
                PivotMDSNodeWrap v = e.Target.AlgorithmData as PivotMDSNodeWrap;
                Edge ee = new Edge(u.node, v.node)
                {
                    Length = e.Length
                };
                g.Edges.Add(ee);
            }
            if (graph.Edges.Count != 0)
            {
                avgLength /= graph.Edges.Count;
            }
            else
            {
                avgLength = 100;
            }

            // create edges from the children of each parent cluster to the parent cluster node
            foreach (Cluster c in graph.RootCluster.AllClustersDepthFirst())
            {
                if (c == graph.RootCluster) continue;

                Node u = new Node(CurveFactory.CreateRectangle(10, 10, new Point()))
                {
                    UserData = c
                };
                c.AlgorithmData = new PivotMDSNodeWrap(u);
                g.Nodes.Add(u);
                    
                foreach (Node v in c.Nodes.Concat(from cc in c.Clusters select (Node)cc))
                {
                    PivotMDSNodeWrap vv = v.AlgorithmData as PivotMDSNodeWrap;
                    g.Edges.Add(new Edge(u, vv.node)
                    {
                        Length = avgLength
                    });
                }
            }

            // create edges between clusters
            foreach (Edge e in graph.Edges)
            {
                if (e.Source is Cluster || e.Target is Cluster)
                {
                    PivotMDSNodeWrap u = e.Source.AlgorithmData as PivotMDSNodeWrap;
                    PivotMDSNodeWrap v = e.Target.AlgorithmData as PivotMDSNodeWrap;
                    Edge ee = new Edge(u.node, v.node)
                    {
                        Length = e.Length
                    };
                    g.Edges.Add(ee);
                }
            }

            // with 0 majorization iterations we just do PivotMDS
            MdsLayoutSettings settings = new MdsLayoutSettings
            {
                ScaleX = Scale,
                ScaleY = Scale,
                IterationsWithMajorization = 0,
                RemoveOverlaps = false,
                AdjustScale = false
            };

            MdsGraphLayout mdsLayout = new MdsGraphLayout(settings, g);
            RunChildAlgorithm(mdsLayout, 1.0);

            g.UpdateBoundingBox();
            foreach (Node v in graph.Nodes)
            {
                PivotMDSNodeWrap m = v.AlgorithmData as PivotMDSNodeWrap;
                v.Center = m.node.Center;
            }
        }
    }
}
