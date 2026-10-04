using System.Collections.Generic;
using System.Diagnostics;

using MermaidRecognition.Layout.Sugiyama.Core;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    [DebuggerDisplay("({Point.X} {Point.Y})")]
    internal class VisibilityVertex : IComparer<VisibilityEdge> {

        // This member is accessed a lot.  Using a field instead of a property for performance.
        readonly internal Point Point;
        bool _isTerminal;
        bool _isShortestPathTerminal;
        readonly List<VisibilityEdge> _inEdges=[];

        internal List<VisibilityEdge> InEdges => _inEdges;

        readonly RbTree<VisibilityEdge> _outEdges;
        /* VisibilityEdge prev; */

        /// <summary>
        /// this collection is sorted by the target point, in the lexicographical order
        /// </summary>
        internal RbTree<VisibilityEdge> OutEdges => _outEdges;

        internal int Degree => InEdges.Count + OutEdges.Count;
        /// <summary>
        /// needed for shortest path calculations
        /// </summary>
        internal double Distance { get;set;}

        internal bool IsTerminal { get => _isTerminal; set => _isTerminal = value;
        }

        internal bool IsShortestPathTerminal { get => _isShortestPathTerminal; set => _isShortestPathTerminal = value;
        }


        /*
                /// <summary>
                /// needed for shortest path calculations
                /// </summary>        
                internal VisibilityVertex Prev {
                    get {
                        if (prev == null) return null;
                        if(prev.Source==this)
                            return prev.Target;
                        return prev.Source;
                    }
                }

                internal void SetPreviousEdge(VisibilityEdge e) {
                    prev = e;
                }
                */
        internal VisibilityVertex(Point point) {
            _outEdges = new RbTree<VisibilityEdge>(this);
            Point = point;
        }

        /// <summary>
        /// Rounded representation; DebuggerDisplay shows the unrounded form.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Point.ToString();

        /// <summary>
        /// These iterate from the end of the list because List.Remove is linear in
        /// the number of items, so callers have been optimized where possible to
        /// remove only the last or next-to-last edges (but in some cases such as
        /// rectilinear, this optimization isn't always possible).
        /// </summary>
        /// <param name="edge"></param>
        internal void RemoveOutEdge(VisibilityEdge edge) => OutEdges.Remove(edge);

        internal void RemoveInEdge(VisibilityEdge edge) {
            for (int ii = InEdges.Count - 1; ii >= 0; --ii) {
                if (InEdges[ii] == edge) {
                    InEdges.RemoveAt(ii);
                    break;
                }
            }
        }
        /// <summary>
        /// avoiding using delegates in calling RBTree.FindFirst because of the memory allocations
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="targetPoint"></param>
        /// <returns></returns>
        static RBNode<VisibilityEdge> FindFirst(RbTree<VisibilityEdge> tree, Point targetPoint) => FindFirst(tree.Root, tree, targetPoint);

        static RBNode<VisibilityEdge> FindFirst(RBNode<VisibilityEdge> n, RbTree<VisibilityEdge> tree, Point targetPoint)
        {
            if ( n ==  tree.Nil)
            {
                return null;
            }

            RBNode<VisibilityEdge> good = null;
            while (n != tree.Nil)
            {
                n = n.Item.TargetPoint >= targetPoint ? (good = n).left : n.right;
            }

            return good;
        }

        internal bool TryGetEdge(VisibilityVertex target, out VisibilityEdge visEdge) {
            RBNode<VisibilityEdge> node = FindFirst(OutEdges, target.Point);// OutEdges.FindFirst(e => e.TargetPoint >= target.Point); 
            if (node != null) {
                if (node.Item.Target == target) {
                    visEdge = node.Item;
                    return true;
                }
            }
            node = FindFirst(target.OutEdges, Point);// target.OutEdges.FindFirst(e => e.TargetPoint >= Point);
            if (node != null) {
                if (node.Item.Target == this) {
                    visEdge= node.Item;
                    return true;
                }
            }
            visEdge = null;
            return false;
        }

        #region IComparer<VisibilityEdge>
        public int Compare(VisibilityEdge? a, VisibilityEdge? b) { if (a is null) { return b is null ? 0 : -1; } if (b is null) { return 1; } return a.TargetPoint.CompareTo(b.TargetPoint); }
        #endregion // IComparer<VisibilityEdge>

        public void ClearEdges() {
            _outEdges.Clear();
            _inEdges.Clear();
        }
    }
}
