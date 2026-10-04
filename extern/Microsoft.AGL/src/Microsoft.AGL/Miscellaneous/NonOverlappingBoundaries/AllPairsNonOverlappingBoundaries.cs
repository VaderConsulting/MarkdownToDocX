using System;
using System.Collections.Generic;
using Microsoft.AGL.Core.DataStructures;
using System.Diagnostics;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Layout.Incremental;
using Microsoft.AGL.Core.Geometry.RTree;

namespace Microsoft.AGL.Miscellaneous.NonOverlappingBoundaries
{
    /// <summary>
    /// A CvxHull is Convex hull
    /// </summary>
    public abstract class CvxHull : IHull {
        /// <summary>
        /// We have a hierarchy of membership
        /// </summary>
        public ClusterConvexHull Parent { get; protected set; }
        /// <summary>
        /// see IHull
        /// </summary>
        public abstract Point Center { get; }
        /// <summary>
        /// move the center by delta
        /// </summary>
        /// <param name="delta"></param>
        public abstract void MoveCenter(Point delta);
        /// <summary>
        /// see IHull
        /// </summary>
        public abstract RectangleNode<IHull> RectangleNode { get; }
        /// <summary>
        /// 
        /// </summary>
        public abstract double Weight { get; }
        /// <summary>
        /// Gets the boundary translated to the current Center
        /// </summary>
        /// <returns></returns>
        public abstract Polyline TranslatedBoundary();
        /// <summary>
        /// Resolves overlap between this and another CHull by moving on the minimum penetration depth vector
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public double Project(IHull other) {
            CvxHull v = other as CvxHull;
            if (this is ClusterConvexHull c && c.Contains(v))
            {
                return 0;
            }
            if (v is ClusterConvexHull vc && vc.Contains(this)) {
                return 0;
            }
            Debug.Assert(v != null);
            Point pd = PenetrationDepth.PenetrationDepthForPolylines(TranslatedBoundary(), v.TranslatedBoundary());
            if (pd.Length > 0) {
                Point wpd = pd / (Weight + v.Weight);
                MoveCenter(v.Weight * wpd);
                v.MoveCenter(-Weight * wpd);
            }
            return pd.Length;
        }
    }
    /// <summary>
    /// A CvxHull for rectangles
    /// </summary>
    public class RCHull : CvxHull {
        internal Node mNode;
        private Polyline boundary;
        private Polyline translatedBoundary;
        double w2, h2;
        /// <summary>
        /// Center of the node
        /// </summary>
        public override Point Center => mNode.Center;
        /// <summary>
        /// Move by delta
        /// </summary>
        /// <param name="delta"></param>
        public override void MoveCenter(Point delta) => mNode.Center += delta;
        /// <summary>
        /// RectangleNode is used in region queries
        /// </summary>
        public override RectangleNode<IHull> RectangleNode {
            get {
                Rectangle r = new Rectangle(Center.X - w2, Center.Y - h2, Center.X + w2, Center.Y + h2);
                return new RectangleNode<IHull>(this, r);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public override double Weight => ((FiNode)mNode.AlgorithmData).stayWeight;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="padding"></param>
        /// <param name="mNode"></param>
        public RCHull(ClusterConvexHull parent, Node mNode, double padding) {
            //var node = (FastIncrementalLayout.Node)mNode.AlgorithmData;
            //this.w2 = node.width / 2.0 + padding;
            //this.h2 = node.height / 2.0 + padding;
            w2 = mNode.Width / 2.0 + padding;
            h2 = mNode.Height / 2.0 + padding;
            Parent = parent;

            boundary = new Polyline(new Point[]{
                new Point(-w2, -h2),
                new Point(-w2, h2),
                new Point(w2, h2),
                new Point(w2, -h2)
            })
            {
                Closed = true
            };
            translatedBoundary = new Polyline(boundary)
            {
                Closed = true
            };
            this.mNode = mNode;
        }
        /// <summary>
        /// Gets the boundary translated to the current Center
        /// </summary>
        /// <returns></returns>
        public override Polyline TranslatedBoundary() {
            PolylinePoint qq = translatedBoundary.StartPoint;
            for (PolylinePoint pp = boundary.StartPoint; pp != null; pp = pp.Next) {
                qq.Point = pp.Point + Center;
                qq = qq.Next;
            }
            return translatedBoundary;
        }
    }
    /// <summary>
    /// The convex hull of the constituents of a Cluster
    /// </summary>
    public class ClusterConvexHull : CvxHull {
        internal Cluster cluster;
        /// <summary>
        /// The Barycenter of the cluster
        /// </summary>
        public override Point Center => cluster.SetBarycenter();
        /// <summary>
        /// Move contents by delta
        /// </summary>
        /// <param name="delta"></param>
        public override void MoveCenter(Point delta) => cluster.ForEachNode(v => v.Center += delta);
        /// <summary>
        /// 
        /// </summary>
        public override double Weight => cluster.Weight;
        /// <summary>
        /// Bounding box used in region queries
        /// </summary>
        public override RectangleNode<IHull> RectangleNode {
            get {
                Rectangle r = new Rectangle(Center);
                foreach (Node node in cluster.Nodes)
                {
                    r.Add(node.BoundingBox);
                }

                return new RectangleNode<IHull>(this, r);
            }
        }
        /// <summary>
        /// The convex hull of the constituents of a Cluster
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="parent"></param>
        public ClusterConvexHull(Cluster cluster, ClusterConvexHull parent) {
            this.cluster = cluster;
            Parent = parent;
        }
        /// <summary>
        /// Gets the boundary translated to the current Center
        /// </summary>
        /// <returns></returns>
        public override Polyline TranslatedBoundary() => ComputeConvexHull();

        /// <summary>
        /// The convex hull of all the points of all the nodes in the cluster
        /// </summary>
        private Polyline ComputeConvexHull()
        {
            List<Point> points = new List<Point>();
            foreach (Node v in cluster.Nodes) 
            {
                CvxHull r = new RCHull(null, v, 0);
                foreach (PolylinePoint p in r.TranslatedBoundary().PolylinePoints)
                {
                    points.Add(p.Point);
                }
            }

            foreach (Cluster c in cluster.Clusters)
            {
                points.AddRange(new ClusterConvexHull(c, this).TranslatedBoundary());
            }

            return new Polyline(ConvexHull.CalculateConvexHull(points)) {Closed = true};
        }

        /// <summary>
        /// Search hierarchy to check if child is a descendent of this.
        /// </summary>
        /// <param name="child"></param>
        /// <returns>true if child is a descendent of this cluster</returns>
        public bool Contains(CvxHull child) {
            if (child.Parent == null) {
                return false;
            }
            if (child.Parent == this) {
                return true;
            }
            return Contains(child.Parent);
        }
    }
    /// <summary>
    /// Prevents the boundaries of nodes and clusters from overlapping
    /// </summary>
    public class AllPairsNonOverlappingBoundaries : IConstraint {
        List<IHull> hulls = [];
        private void traverseClusters(ClusterConvexHull parent, Cluster cluster, double padding) {
            ClusterConvexHull hull = new ClusterConvexHull(cluster, parent);
            hulls.Add(hull);
            foreach (Node v in cluster.nodes) {
                hulls.Add(new RCHull(hull, v, padding));
            }
            foreach (Cluster c in cluster.clusters) {
                traverseClusters(hull, c, padding);
            }
        }
        /// <summary>
        /// 
        /// Non-overlap between nodes in the same cluster (or the root cluster), between the convex hulls
        /// of clusters and nodes that do belong to those clusters and between clusters and clusters.
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="settings">for padding extra space around nodes</param>
        public AllPairsNonOverlappingBoundaries(Cluster cluster, FastIncrementalLayoutSettings settings) {
            foreach (Node v in cluster.nodes) {
                hulls.Add(new RCHull(null,v, settings.NodeSeparation));
            }
            foreach (Cluster c in cluster.clusters) {
                traverseClusters(null, c, settings.NodeSeparation);
            }
        }
        #region IConstraint Members
        private static int AllPairsComputationLimit = 20;
        /// <summary>
        /// Uses Lev's fast proximity query to find pairs of nodes/clusters with overlapping bounding boxes.
        /// When such are found, they are projected apart.
        /// </summary>
        public double Project() {
            double displacement = 0;
            if (hulls.Count < AllPairsComputationLimit) {
                // if there are only a few nodes then do it the most straightforward n^2 way
                for (int i = 0; i < hulls.Count - 1; ++i) {
                    IHull u = hulls[i];
                    for (int j = i + 1; j < hulls.Count; ++j) {
                        displacement += u.Project(hulls[j]);
                    }
                }
            } else {
                ProximityQuery pq = new ProximityQuery(hulls);
                List<Tuple<IHull, IHull>> closePairs = pq.GetAllIntersections();
                //shuffle(ref closePairs);
                foreach (Tuple<IHull, IHull> k in closePairs) {
                    displacement += k.Item1.Project(k.Item2);
                }
            }
            return displacement;
        }

        //void shuffle<T>(ref List<T> l) {
        //    List<T> tmpList = new List<T>();
        //    while (l.Count > 0) {
        //        int i = rand.Next(l.Count);
        //        tmpList.Add(l[i]);
        //        l.RemoveAt(i);
        //    }
        //    l = tmpList;
        //}
        /// <summary>
        /// NonOverlap constraints are a beautification thing, and therefore higher level than others
        /// </summary>
        /// <returns>2</returns>
        public int Level => 2;
        #endregion

        #region IConstraint Members


        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<Node> Nodes => new List<Node>();

        #endregion
    }
}
