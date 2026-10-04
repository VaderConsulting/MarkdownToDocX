using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using Microsoft.AGL.Core;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Layout.Incremental;
using Microsoft.AGL.Layout.Layered;
using Microsoft.AGL.Layout.MDS;

//using Microsoft.AGL.Routing;

namespace Microsoft.AGL.Layout.Initial
{
    /// <summary>
    /// Methods for obtaining an initial layout of a graph using various means.
    /// </summary>
    public class InitialLayout : AlgorithmBase
    {
        private GeometryGraph graph;
        private FastIncrementalLayoutSettings settings;

        private int componentCount;

        /// <summary>
        /// Set to true if the graph specified is a single connected component with no clusters
        /// </summary>
        public bool SingleComponent { get; set; }

        /// <summary>
        /// Static layout of graph by gradually adding constraints.
        /// Uses PivotMds to find initial layout.
        /// Breaks the graph into connected components (nodes in the same cluster are considered
        /// connected whether or not there is an edge between them), then lays out each component
        /// individually.  Finally, a simple packing is applied.
        /// ratio as close as possible to the PackingAspectRatio property (not currently used).
        /// </summary>
        public InitialLayout(GeometryGraph graph, FastIncrementalLayoutSettings settings)
        {
            ValidateArg.IsNotNull(graph, "graph");
            ValidateArg.IsNotNull(settings, "settings");
            this.graph = graph;

            this.settings = new FastIncrementalLayoutSettings(settings)
            {
                ApplyForces = true,
                InterComponentForces = true,
                RungeKuttaIntegration = false,
                RespectEdgePorts = false
            };
        }

        /// <summary>
        /// The actual layout process
        /// </summary>
        protected override void RunInternal()
        {
            if (SingleComponent)
            {
                componentCount = 1;
                LayoutComponent(graph);
            }
            else
            {
                foreach (Cluster c in graph.RootCluster.AllClustersDepthFirst())
                {
                    if (c == graph.RootCluster || c.RectangularBoundary == null) continue;
                    c.RectangularBoundary.GenerateFixedConstraints = false;
                }

                List<GeometryGraph> components = graph.GetClusteredConnectedComponents().ToList();

                componentCount = components.Count;

                foreach (GeometryGraph component in components)
                {
                    LayoutComponent(component);
                }

                graph.BoundingBox = MdsGraphLayout.PackGraphs(components, settings);
                ProgressComplete();

                // update positions of original graph elements
                foreach (Node v in graph.Nodes)
                {
                    GraphConnectedComponents.AlgorithmDataNodeWrap copy = v.AlgorithmData as GraphConnectedComponents.AlgorithmDataNodeWrap;
                    Debug.Assert(copy != null);
                    v.Center = copy.node.Center;
                }

                foreach (Edge e in graph.Edges)
                {
                    if (e.AlgorithmData is Edge copy)
                    {
                        e.EdgeGeometry = copy.EdgeGeometry;
                        e.EdgeGeometry.Curve = copy.Curve;
                    }
                }

                foreach (Cluster c in graph.RootCluster.AllClustersDepthFirst().Where(c => c != graph.RootCluster))
                {
                    GraphConnectedComponents.AlgorithmDataNodeWrap copy = c.AlgorithmData as GraphConnectedComponents.AlgorithmDataNodeWrap;
                    Cluster copyCluster = copy.node as Cluster;
                    Debug.Assert(copyCluster != null);
                    c.RectangularBoundary = copyCluster.RectangularBoundary;
                    c.RectangularBoundary.GenerateFixedConstraints = c.RectangularBoundary.GenerateFixedConstraintsDefault;
                    c.BoundingBox = c.RectangularBoundary.Rect;
                    c.RaiseLayoutDoneEvent();
                }
            }
        }

        private void LayoutComponent(GeometryGraph component)
        {
            if (component.Nodes.Count > 1 || component.RootCluster.Clusters.Any())
            {
                // for small graphs (below 100 nodes) do extra iterations
                settings.MaxIterations = LayoutAlgorithmHelpers.NegativeLinearInterpolation(
                    component.Nodes.Count,
                    /*lowerThreshold:*/ 50, /*upperThreshold:*/ 500, /*minIterations:*/ 5, /*maxIterations:*/ 10);
                settings.MinorIterations = LayoutAlgorithmHelpers.NegativeLinearInterpolation(component.Nodes.Count,
                    /*lowerThreshold:*/ 50, /*upperThreshold:*/ 500, /*minIterations:*/ 3, /*maxIterations:*/ 20);

                if (settings.MinConstraintLevel == 0)
                {
                    // run PivotMDS with a largish Scale so that the layout comes back oversized.
                    // subsequent incremental iterations do a better job of untangling when they're pulling it in
                    // rather than pushing it apart.
                    PivotMDS pivotMDS = new PivotMDS(component) { Scale = 2 };
                    RunChildAlgorithm(pivotMDS, 0.5 / componentCount);
                }
                FastIncrementalLayout fil = new FastIncrementalLayout(component, settings, settings.MinConstraintLevel, anyCluster => settings);
                Debug.Assert(settings.Iterations == 0);

                foreach (int level in GetConstraintLevels(component))
                {
                    if (level > settings.MaxConstraintLevel)
                    {
                        break;
                    }
                    if (level > settings.MinConstraintLevel)
                    {
                        fil.CurrentConstraintLevel = level;
                    }
                    do
                    {
                        fil.Run();
                    } while (!settings.IsDone);
                }
            }

            // Pad the graph with margins so the packing will be spaced out.
            component.Margins = settings.NodeSeparation;
            component.UpdateBoundingBox();

            // Zero the graph
            component.Translate(-component.BoundingBox.LeftBottom);
        }

        /// <summary>
        /// Get the distinct ConstraintLevels that need to be applied to layout.
        /// Used by InitialLayout.
        /// Will only include ConstraintLevel == 1 if there are structural constraints
        /// Will only include ConstraintLevel == 2 if AvoidOverlaps is on and there are fewer than 2000 nodes
        /// </summary>
        /// <returns>0, 1 or 2</returns>
        IEnumerable<int> GetConstraintLevels(GeometryGraph component)
        {
            List<int> keys = (from c in settings.StructuralConstraints select c.Level).ToList();
            keys.Add(0);
            if (settings.IdealEdgeLength.EdgeDirectionConstraints != Directions.None) { 
                keys.Add(1); 
            }
            if (settings.AvoidOverlaps && component.Nodes.Count < 2000) { keys.Add(2); }
            return keys.Distinct();
        }

    }
}
