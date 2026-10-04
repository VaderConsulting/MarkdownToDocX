using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.OverlapRemoval;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Incremental {
    /// <summary>
    /// Fix the position of a node.
    /// Create locks using FastIncrementalLayoutSettings.CreateLock method.
    /// </summary>
    public class LockPosition : IConstraint {
        private double weight = 1e6;
        internal Node node;
        internal LinkedListNode<LockPosition> listNode;
        /// <summary>
        /// Makes a constraint preserve the nodes' bounding box with a very large weight
        /// </summary>
        /// <param name="node"></param>
        /// <param name="bounds"></param>
        internal LockPosition(Node node, Rectangle bounds) {
            this.node = node;
            Bounds = bounds;
        }
        /// <summary>
        /// Makes a constraint to preserve the nodes' position with the specified weight
        /// </summary>
        /// <param name="node"></param>
        /// <param name="bounds"></param>
        /// <param name="weight"></param>
        internal LockPosition(Node node, Rectangle bounds, double weight)
            : this(node, bounds) => Weight = weight;

        /// <summary>
        /// Set the weight for this lock constraint, i.e. if this constraint conflicts with some other constraint,
        /// projection of that constraint be biased by the weights of the nodes involved
        /// </summary>
        public double Weight
        {
            get => weight;
            set
            {
                if (value > 1e20)
                {
                    throw new ArgumentOutOfRangeException("value", "must be < 1e10 or we run out of precision");
                }
                if (value < 1e-3)
                {
                    throw new ArgumentOutOfRangeException("value", "must be > 1e-3 or we run out of precision");
                }
                weight = value;
            }
        }
        /// <summary>
        /// This assigns the new bounds and needs to be called after Solve() because
        /// multiple locked nodes may cause each other to move.
        /// </summary>
        public Rectangle Bounds {
            get;
            set;
        }
        /// <summary>
        /// By default locks are not sticky and their ideal Position gets updated when they are pushed by another node.
        /// Making them Sticky causes the locked node to spring back to its ideal Position when whatever was pushing it
        /// slides past.
        /// </summary>
        public bool Sticky {
            get;
            set;
        }
        /// <summary>
        /// Move node (or cluster + children) to lock position
        /// I use stay weight in "project" of any constraints involving the locked node
        /// </summary>
        public virtual double Project() {
            Point delta = Bounds.LeftBottom - node.BoundingBox.LeftBottom;
            double deltaLength = delta.Length;
            double displacement = deltaLength;
            if (node is Cluster cluster)
            {
                foreach (Cluster c in cluster.AllClustersDepthFirst())
                {
                    foreach (Node v in c.Nodes)
                    {
                        v.Center += delta;
                        displacement += deltaLength;
                    }
                    if (c == cluster)
                    {
                        cluster.RectangularBoundary.Rect = Bounds;
                    }
                    else
                    {
                        Rectangle r = c.RectangularBoundary.Rect;
                        c.RectangularBoundary.Rect = new Rectangle(r.LeftBottom + delta, r.RightTop + delta);
                    }
                }
            }
            else
            {
                node.BoundingBox = Bounds;
            }
            return displacement;
        }
        /// <summary>
        /// LockPosition is always applied (level 0)
        /// </summary>
        /// <returns>0</returns>
        public int Level => 0;

        /// <summary>
        /// Sets the weight of the node (the FINode actually) to the weight required by this lock.
        /// If the node is a Cluster then:
        ///  - its boundaries are locked
        ///  - all of its descendant nodes have their lock weight set
        ///  - all of its descendant clusters are set to generate fixed constraints (so they don't get squashed)
        /// Then, the node (or clusters) parents (all the way to the root) have their borders set to generate unfixed constraints 
        /// (so that this node can move freely inside its ancestors
        /// </summary>
        internal void SetLockNodeWeight()
        {
            if (node is Cluster cluster)
            {
                RectangularClusterBoundary cb = cluster.RectangularBoundary;
                cb.Lock(Bounds.Left, Bounds.Right, Bounds.Top, Bounds.Bottom);
                foreach (Cluster c in cluster.AllClustersDepthFirst())
                {
                    c.RectangularBoundary.GenerateFixedConstraints = true;
                    foreach (Node child in c.Nodes)
                    {
                        SetFINodeWeight(child, weight);
                    }
                }
            }
            else
            {
                SetFINodeWeight(node, weight);
            }
            foreach (Cluster ancestor in node.AllClusterAncestors)
            {
                if (ancestor.RectangularBoundary != null)
                {
                    ancestor.RectangularBoundary.GenerateFixedConstraints = false;
                }
                ancestor.UnsetInitialLayoutState();
            }
        }

        /// <summary>
        /// Reverses the changes made by SetLockNodeWeight
        /// </summary>
        internal void RestoreNodeWeight() {
            if (node is Cluster cluster)
            {
                cluster.RectangularBoundary.Unlock();
                foreach (Cluster c in cluster.AllClustersDepthFirst())
                {
                    c.RectangularBoundary.GenerateFixedConstraints = c.RectangularBoundary.GenerateFixedConstraintsDefault;
                    foreach (Node child in c.Nodes)
                    {
                        SetFINodeWeight(child, 1);
                    }
                }
            }
            else
            {
                SetFINodeWeight(node, 1);
            }
            Cluster parent = node.ClusterParents.FirstOrDefault();
            while (parent != null)
            {
                if (parent.RectangularBoundary != null)
                {
                    parent.RectangularBoundary.GenerateFixedConstraints = parent.RectangularBoundary.GenerateFixedConstraintsDefault;
                }
                parent = parent.ClusterParents.FirstOrDefault();
            }
        }

        private static void SetFINodeWeight(Node child, double weight)
        {
            if (child.AlgorithmData is FiNode v)
            {
                v.stayWeight = weight;
            }
        }

        /// <summary>
        /// Get the list of nodes involved in the constraint
        /// </summary>
        public IEnumerable<Node> Nodes
        {
            get
            {
                List<Node> nodes = new List<Node>();
                if (node is Cluster cluster)
                {
                    cluster.ForEachNode(nodes.Add);
                }
                else
                {
                    nodes.Add(node);
                }
                return nodes;
            }
        }
    }
}