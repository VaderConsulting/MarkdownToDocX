using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Routing
{
    /// <summary>
    /// Class for creating Shape elements from a Graph.
    /// </summary>
    static internal class ShapeCreator
    {
        /// <summary>
        /// For a given graph finds the obstacles for nodes and clusters, correctly parenting the obstacles
        /// according to the cluster hierarchy
        /// </summary>
        /// <param name="graph">graph with edges to route and nodes/clusters to route around</param>
        /// <returns>the set of obstacles with correct cluster hierarchy and ports</returns>
        public static IEnumerable<Shape> GetShapes(GeometryGraph graph)
        {
            Dictionary<Node, Shape> nodesToShapes = new Dictionary<Node, Shape>();
            Node[] interestingNodes = graph.Nodes.Where(n => !n.UnderCollapsedCluster()).ToArray();
            foreach (Node? v in interestingNodes)
            {
                nodesToShapes[v] = CreateShapeWithCenterPort(v);
            }

            foreach (Cluster c in graph.RootCluster.AllClustersDepthFirst()) {
                if (!c.IsCollapsed)
                {
                    foreach (Node v in c.Nodes)
                    {
                        if (!nodesToShapes.ContainsKey(v))
                        {
                            nodesToShapes[v] = CreateShapeWithCenterPort(v);
                        }
                    }
                }

                if (c == graph.RootCluster)
                {
                    continue;
                }

                Shape parent = nodesToShapes[c] = CreateShapeWithClusterBoundaryPort(c);
                if (c.IsCollapsed)
                {
                    continue;
                }

                foreach (Node v in c.Nodes)
                {
                    parent.AddChild(nodesToShapes[v]);
                }

                foreach (Cluster d in c.Clusters)
                {
                    parent.AddChild(nodesToShapes[d]);
                }
            }

            foreach (Edge edge in graph.Edges) {
                if (nodesToShapes.TryGetValue(edge.Source, out Shape? shape))
                {
                    if (edge.SourcePort != null)
                    {
                        shape.Ports.Insert(edge.SourcePort);
                    }
                }
                if (nodesToShapes.TryGetValue(edge.Target, out shape)) {
                    if (edge.TargetPort != null)
                    {
                        shape.Ports.Insert(edge.TargetPort);
                    }
                }
                
            }

            return nodesToShapes.Values;
        }

        

        /// <summary>
        /// Creates a shape with a RelativeFloatingPort for the node center, attaches it to the shape and all edges
        /// </summary>
        /// <param name="node"></param>
        /// <returns>Shape obstacle for the node with simple port</returns>
        static Shape CreateShapeWithCenterPort(Node node)
        {
            // Debug.Assert(ApproximateComparer.Close(node.BoundaryCurve.BoundingBox, node.BoundingBox), "node's curve doesn't fit its bounds!");
            RelativeShape shape = new RelativeShape(() => node.BoundaryCurve);
            RelativeFloatingPort port = new RelativeFloatingPort(() => node.BoundaryCurve, () => node.Center);
            shape.Ports.Insert(port);
            foreach (Edge e in node.InEdges)
            {
                FixPortAtTarget(shape, port, e);
            }

            foreach (Edge e in node.OutEdges)
            {
                FixPortAtSource(shape, port, e);
            }

            foreach (Edge e in node.SelfEdges) {
               FixPortAtSource(shape, port, e);
               FixPortAtTarget(shape, port, e);
            }
#if DEBUG
           // shape.UserData = node.ToString();
#endif
            return shape;
        }
        /// <summary>
        /// Creates a ClusterBoundaryPort for the cluster boundary, attaches it to the shape and all edges
        /// </summary>
        /// <param name="node"></param>
        /// <returns>Shape obstacle for the node with simple port</returns>
        static Shape CreateShapeWithClusterBoundaryPort(Node node) {
            // Debug.Assert(ApproximateComparer.Close(node.BoundaryCurve.BoundingBox, node.BoundingBox), "node's curve doesn't fit its bounds!");
            Debug.Assert(node is Cluster);
            RelativeShape shape = new RelativeShape(() => node.BoundaryCurve);
            ClusterBoundaryPort port = new ClusterBoundaryPort(()=>node.BoundaryCurve, ()=>node.Center);
            shape.Ports.Insert(port);
            foreach (Edge e in node.InEdges)
            {
                FixPortAtTarget(shape, port, e);
            }

            foreach (Edge e in node.OutEdges)
            {
                FixPortAtSource(shape, port, e);
            }

            foreach (Edge e in node.SelfEdges) {
                FixPortAtSource(shape, port, e);
                FixPortAtTarget(shape, port, e);
            }
#if DEBUG
            shape.UserData = node.ToString();
#endif
            return shape;
        }

        static void FixPortAtSource(Shape shape, Port port, Edge e) {
            if (e.SourcePort == null)
            {
                e.SourcePort = port;
            }
            else
            {
                shape.Ports.Insert(e.SourcePort);
            }
        }

        static void FixPortAtTarget(Shape shape, Port port, Edge e) {
            if (e.TargetPort == null)
            {
                e.TargetPort = port;
            }
            else
            {
                shape.Ports.Insert(e.TargetPort);
            }
        }
    }
}
