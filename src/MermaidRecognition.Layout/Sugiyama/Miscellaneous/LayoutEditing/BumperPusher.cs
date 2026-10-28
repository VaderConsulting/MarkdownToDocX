using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;

namespace MermaidRecognition.Layout.Sugiyama.Miscellaneous.LayoutEditing {
    /// <summary>
    /// pushes the nodes it got bumped to: pushes horizontally or vertically
    /// </summary>
    public class BumperPusher {
        readonly double separation;
        readonly Set<Node> fixedNodes = [];
        readonly RTree<Node> rtree;
        Node[] pushingNodes;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pushedNodes">nodes that are being pushed</param>
        /// <param name="separation"></param>
        /// <param name="pushingNodes"></param>
        public BumperPusher(IEnumerable<Node> pushedNodes, double separation, Node[] pushingNodes) {
            this.separation = separation;
            rtree = new RTree<Node>(RectangleNode<Node>.CreateRectangleNodeOnEnumeration(
                pushedNodes.Select(n => new RectangleNode<Node>(n, GetPaddedBoxOfNode(n)))));

            //LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(rtree.GetAllLeaves().Select(n=>new DebugCurve(n.BoundaryCurve)));
            this.pushingNodes = pushingNodes;
        }

        internal IEnumerable<Node> FixedNodes => fixedNodes;

        Rectangle GetPaddedBoxOfNode(Node n) {
            Rectangle ret = n.BoundaryCurve.BoundingBox;
            ret.Pad(separation/2);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IList<Node> PushNodes() {
            fixedNodes.Clear();
            fixedNodes.InsertRange(pushingNodes);

            Queue<Node> q = new Queue<Node>(pushingNodes);
            List<Node> ret = [];
            while (q.Count > 0) {
                Node n = q.Dequeue();
                foreach (Node node in PushByNodeAndReportPushedAsFixed(n)) {
                    q.Enqueue(node);
                    fixedNodes.Insert(node);
                    ret.Add(node);                    
                }
            }
            return ret;
        }

        IEnumerable<Node> PushByNodeAndReportPushedAsFixed(Node pushingNode) {
            List<Node> ret = [];
            Rectangle pushingNodeBox = GetPaddedBoxOfNode(pushingNode);
            foreach (RectangleNode<Node> rectNode in rtree.GetAllLeavesIntersectingRectangle(pushingNodeBox)) {
                if (fixedNodes.Contains(rectNode.UserData))
                {
                    continue;
                }

                if (PushNodeAndUpdateRTree(pushingNode, rectNode))
                {
                    ret.Add(rectNode.UserData);
                }
            }
            return ret;
        }

        bool PushNodeAndUpdateRTree(Node pushingNode, RectangleNode<Node> pushed) {
            Point del = pushed.UserData.Center - pushingNode.Center;
            double w = (pushingNode.Width / 2) + (pushed.UserData.Width / 2);
            double h = (pushingNode.Height / 2) + (pushed.UserData.Height / 2);
            double absDelXBetweenCenters = Math.Abs(del.X);
            double absDelYBetweenCenters = Math.Abs(del.Y);

            double xSep = absDelXBetweenCenters - w;
            double ySep = absDelYBetweenCenters - h;
            if (xSep >= separation || ySep >= separation)
            {
                return false;
            }

            if (absDelXBetweenCenters >= absDelYBetweenCenters) {
                double d = del.X > 0 ? separation - xSep : xSep - separation;
                PushByX(d, pushed);
            }
            else {
                double d = del.Y > 0 ? separation - ySep : ySep - separation;
                PushByY(d, pushed);
            }
            UpdateBoundingBoxesOfPushedAndUpParents(pushed);
            return true;
        }
        

        void PushByX(double del, RectangleNode<Node> pushed) {
            Point delPoint = new Point(del, 0);
            PushByPoint(pushed, delPoint);
        }

        static void PushByPoint(RectangleNode<Node> pushed, Point delPoint) {
            pushed.UserData.Center += delPoint;
            if (pushed.UserData is Cluster cluster)
            {
                cluster.DeepContentsTranslation(delPoint, true);
            }
        }

        void PushByY(double del, RectangleNode<Node> pushed) {
            Point delPoint = new Point(0, del);
            PushByPoint(pushed, delPoint);
        }

        void UpdateBoundingBoxesOfPushedAndUpParents(RectangleNode<Node> pushed) {
            pushed.Rectangle = GetPaddedBoxOfNode(pushed.UserData);
            RectangleNode<Node> parent = pushed.Parent;
            while (parent != null) {
                parent.Rectangle = new Rectangle(parent.Left.Rectangle, parent.Right.Rectangle);
                parent = parent.Parent;
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="previousBox"></param>
        public void UpdateRTreeByChangedNodeBox(Node cluster, Rectangle previousBox) {
            RectangleNode<Node> rectNode = FindClusterNode(cluster, previousBox);
            UpdateBoundingBoxesOfPushedAndUpParents(rectNode);
        }

        RectangleNode<Node> FindClusterNode(Node cluster, Rectangle previousBox) {
            RectangleNode<Node> node = rtree.RootNode;
            return FindClusterNodeRecurse(node, cluster, previousBox);
        }

        RectangleNode<Node> FindClusterNodeRecurse(RectangleNode<Node> node, Node cluster, Rectangle previousBox) {
            if (node.UserData != null)
            {
                return node.UserData == cluster ? node : null;
            }

            RectangleNode<Node> n0=null;
            if (previousBox.Intersects(node.Left.Rectangle))
            {
                n0 = FindClusterNodeRecurse(node.Left, cluster, previousBox);
            }

            if (n0 != null)
            {
                return n0;
            }

            if (previousBox.Intersects(node.Right.Rectangle))
            {
                return FindClusterNodeRecurse(node.Right, cluster, previousBox);
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Node FirstPushingNode() => pushingNodes[0];
    }
}