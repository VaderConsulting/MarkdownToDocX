using System;
using System.Linq;
using Microsoft.AGL.Core;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Core.Layout.ProximityOverlapRemoval.MinimumSpanningTree;
using Microsoft.AGL.Layout.MDS;
using Microsoft.AGL.Routing;
using Microsoft.AGL.Routing.Rectilinear;

namespace Microsoft.AGL.Prototype.Ranking {
    /// <summary>
    /// Ranking layout for directed graphs.
    /// </summary>
    public class RankingLayout : AlgorithmBase {
        GeometryGraph graph;

         RankingLayoutSettings settings;

        /// <summary>
        /// Constructs the ranking layout algorithm.
        /// </summary>
        public RankingLayout(RankingLayoutSettings settings, GeometryGraph geometryGraph)
        {
            this.settings = settings;
            this.graph = geometryGraph;
        }

         void SetNodePositionsAndMovedBoundaries() {
            
            int pivotNumber = Math.Min(graph.Nodes.Count,settings.PivotNumber);
            double scaleX = settings.ScaleX;
            double scaleY = settings.ScaleY;
          
            int[] pivotArray = new int[pivotNumber];
            PivotDistances pivotDistances = new PivotDistances(graph, false, pivotArray);
            pivotDistances.Run();
            double[][] c = pivotDistances.Result;
            double[] x, y;
            MultidimensionalScaling.LandmarkClassicalScaling(c, out x, out y, pivotArray);

            Standardize(x);
            double[] p = Centrality.PageRank(graph, .85, false);
            // double[] q = Centrality.PageRank(graph, .85, true);
            Standardize(p);
            // Standardize(q);

            int index = 0;
            foreach (Node node in graph.Nodes) {
                node.Center = new Point((int) (x[index]*scaleX), (int) (Math.Sqrt(p[index])*scaleY));
                index++;
            }

            OverlapRemoval.RemoveOverlaps(graph.Nodes.ToArray(), settings.NodeSeparation);
        }

        /// <summary>
        /// Executes the algorithm.
        /// </summary>
        protected override void RunInternal() {
            SetNodePositionsAndMovedBoundaries();
            StraightLineEdges.SetStraightLineEdgesWithUnderlyingPolylines(graph);
            SetGraphBoundingBox();
        }

         void SetGraphBoundingBox() {
            graph.BoundingBox = graph.PumpTheBoxToTheGraphWithMargins();
        }

        /// <summary>
        /// Scales and translates a vector so that all values are exactly between 0 and 1.
        /// </summary>
        /// <param name="x">Vector to be standardized.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "x")]
        static void Standardize(double[] x)
        {
            double min = Double.PositiveInfinity;
            double max = Double.NegativeInfinity;
            for (int i = 0; i < x.Length; i++)
            {
                max = Math.Max(max, x[i]);
                min = Math.Min(min, x[i]);
            }
            for (int i = 0; i < x.Length; i++)
            {
                x[i] = (x[i] - min) / (max - min);
            }
        }
    }
}
