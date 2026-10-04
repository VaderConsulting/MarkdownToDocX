using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Layout.LargeGraphLayout;

#if TEST_MSAGL
using Microsoft.AGL.DebugHelpers;
using System.Diagnostics;
#endif

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// This class keeps the graph nodes, edges, and clusters, together with their geometries
    /// </summary>
#if TEST_MSAGL
    
#endif
    public class GeometryGraph : GeometryObject {
         IList<Node> nodes;
         EdgeCollection edges;
#if TEST_MSAGL
    [NonSerialized]
#endif
         Cluster rootCluster;

        /// <summary>
        /// Creates a new GeometryGraph.
        /// </summary>
        public GeometryGraph()
        {
            nodes = new NodeCollection(this);
            edges = new EdgeCollection(this);
            rootCluster = new Cluster();
        }

        /// <summary>
        /// The root cluster for this graph. Will never be null.
        /// </summary>
        public Cluster RootCluster
        {
            get => rootCluster;
            set
            {
                ValidateArg.IsNotNull(value, "value");
                rootCluster = value;
            }
        }

        internal Rectangle boundingBox;

        /// <summary>
        /// Bounding box of the graph
        /// </summary>
        public override Rectangle BoundingBox { get => boundingBox; set => boundingBox = value;
        }

        double margins;
#if DEBUG && TEST_MSAGL
        /// <summary>
        /// curves to show debug stuff
        /// </summary>
        public DebugCurve[] DebugCurves;
#endif

        /// <summary>
        /// margins width are equal from the left and from the right; they are given in percents
        /// </summary>
        public double Margins
        { get => margins; set => margins = value;
        }

        /// <summary>
        /// Width of the graph
        /// </summary>
        public double Width => BoundingBox.RightBottom.X - BoundingBox.LeftTop.X;

        /// <summary>
        /// Height of the graph
        /// </summary>
        public double Height => BoundingBox.Height;

        /// <summary>
        /// Left bound of the graph
        /// </summary>
        public double Left => BoundingBox.Left;

        /// <summary>
        /// Right bound of the graph
        /// </summary>
        public double Right => BoundingBox.Right;

        /// <summary>
        /// Left bottom corner of the graph
        /// </summary>
        internal Point LeftBottom => new Point(BoundingBox.Left, BoundingBox.Bottom);

        /// <summary>
        /// Right top corner of the graph
        /// </summary>
        internal Point RightTop => new Point(Right, Top);

        /// <summary>
        /// Bottom bound of the graph
        /// </summary>
        public double Bottom => BoundingBox.Bottom;

        /// <summary>
        /// Top bound of the graph
        /// </summary>
        public double Top => BoundingBox.Bottom + BoundingBox.Height;

        /// <summary>
        /// The nodes in the graph.
        /// </summary>
        public IList<Node> Nodes { get => nodes; set => nodes = value;
        }

        /// <summary>
        /// Edges of the graph
        /// </summary>
        public EdgeCollection Edges { get => edges; set => edges = value;
        }

        /// <summary>
        /// Returns a collection of all the labels in the graph.
        /// </summary>
        /// <returns></returns>
        public ICollection<Label> CollectAllLabels() => Edges.SelectMany(e => e.Labels).ToList();

        /// <summary>
        /// transforms the graph by the given matrix
        /// </summary>
        /// <param name="matrix">the matrix</param>
        public void Transform(PlaneTransformation matrix) {
            foreach (Node node in Nodes)
                node.Transform(matrix);
            foreach (Edge edge in Edges)
                edge.Transform(matrix);
#if DEBUG && TEST_MSAGL
            if (DebugCurves != null)
                foreach (DebugCurve dc in DebugCurves)
                    dc.Curve = dc.Curve.Transform(matrix);
#endif
            UpdateBoundingBox();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Rectangle PumpTheBoxToTheGraphWithMargins() {
            Rectangle b = Rectangle.CreateAnEmptyBox();
            PumpTheBoxToTheGraph(ref b);
            Point del =new Point(Margins, -Margins);
            b.RightBottom += del;
            b.LeftTop -= del;
            b.Width = Math.Max(b.Width, MinimalWidth);
            b.Height = Math.Max(b.Height, MinimalHeight);

            return b;
        }

        ///<summary>
        ///the minimal width of the graph
        ///</summary>
        public double MinimalWidth { get; set; }
        ///<summary>
        ///the minimal height of the graph
        ///</summary>
        public double MinimalHeight { get; set; }
        /// <summary>
        /// enlarge the rectangle to contain the graph
        /// </summary>
        /// <param name="b"></param>
        void PumpTheBoxToTheGraph(ref Rectangle b) {
            foreach (Edge e in Edges) {
                if (e.UnderCollapsedCluster()) continue;
                if (e.Curve != null) {
                    Rectangle cb = e.Curve.BoundingBox;
                    cb.Pad(e.LineWidth);
                    b.Add(cb);
                }

                foreach (Label l in e.Labels.Where(lbl => lbl != null))
                    b.Add(l.BoundingBox);
            }

            foreach (Node n in Nodes) {
                if (n.UnderCollapsedCluster()) continue;
                b.Add(n.BoundingBox);
            }


            foreach (Cluster c in RootCluster.Clusters) {
                if (c.BoundaryCurve == null) {
                    if (c.RectangularBoundary != null)
                        c.BoundaryCurve = c.RectangularBoundary.RectangularHull();
                }
                if (c.BoundaryCurve != null)
                    b.Add(c.BoundaryCurve.BoundingBox);
            }
#if DEBUG && TEST_MSAGL
            if(DebugCurves!=null)
                foreach (DebugCurve debugCurve in DebugCurves.Where(d => d.Curve != null))
                    b.Add(debugCurve.Curve.BoundingBox);
#endif 
        }

        /// <summary>
        /// Translates the graph by delta.
        /// Assumes bounding box is already up to date.
        /// </summary>
        public void Translate(Point delta)
        {
            Set<Node> nodeSet = new Set<Node>(Nodes);
            foreach (Node v in Nodes)
                v.Center += delta;

            foreach (Cluster cluster in RootCluster.AllClustersDepthFirstExcludingSelf()) {
                foreach (Node node in cluster.Nodes.Where(n => !nodeSet.Contains(n)))
                    node.Center += delta;
                cluster.Center += delta;
                cluster.RectangularBoundary.TranslateRectangle(delta);                
            }

            foreach (Edge e in edges)
                e.Translate(delta);

            BoundingBox = new Rectangle(BoundingBox.Left + delta.X, BoundingBox.Bottom + delta.Y, new Point(BoundingBox.Width, BoundingBox.Height));
        }

        /// <summary>
        /// Updates the bounding box to fit the contents.
        /// </summary>
        public void UpdateBoundingBox() => BoundingBox = PumpTheBoxToTheGraphWithMargins();

        /// <summary>
        /// Flatten the list of nodes and clusters
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Node> GetFlattenedNodesAndClusters()
        {
            foreach (Node v in Nodes)
            {
                yield return v;
            }
            
            foreach(Cluster cluster in RootCluster.AllClustersDepthFirst())
            {
                if (cluster != RootCluster)
                {
                    yield return cluster;
                }
            }
        }

        /// <summary>
        /// Finds the first node with the corresponding user data.
        /// </summary>
        /// <returns>The first node with the given user data. Null if no such node exists.</returns>
        public Node FindNodeByUserData(object userData) => Nodes.FirstOrDefault(n => n.UserData.Equals(userData));
#if TEST_MSAGL
        ///<summary>
        ///</summary>

        public void SetDebugIds()
        {
            int id = 0;
            foreach (Cluster node in RootCluster.AllClustersDepthFirst())
                node.DebugId = id++;

            foreach (Node node in Nodes)
                if (node.DebugId == null)
                    node.DebugId = id++;
        }

        internal void CheckClusterConsistency() {
            foreach (Cluster cluster in RootCluster.AllClustersDepthFirst())
                CheckClusterConsistency(cluster);
        }

        static void CheckClusterConsistency(Cluster cluster) {
            if (cluster.BoundaryCurve == null)
                return;
            foreach (Node child in cluster.Clusters.Concat(cluster.Nodes)) {
                bool inside =Curve.CurveIsInsideOther(child.BoundaryCurve, cluster.BoundaryCurve);
#if TEST_MSAGL && DEBUG
//                if (!inside)
//                    LayoutAlgorithmSettings.ShowDebugCurves(new DebugCurve("green", cluster.BoundaryCurve), new DebugCurve("red", child.BoundaryCurve));
#endif
                Debug.Assert(inside,
                             "A child of a cluster has to have the BoundaryCurve inside of the BoundaryCurve of the cluster");
            }

        }
#endif
        /// <summary>
        /// info of layers for large graph browsing
        /// </summary>
        public LgData LgData { get; set; }
    }
}
