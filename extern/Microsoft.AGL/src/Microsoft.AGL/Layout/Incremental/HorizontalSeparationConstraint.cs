using System.Collections.Generic;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.Incremental {
    /// <summary>
    /// A horizontal separation constraint requires a minimum separation between x coordinates of two nodes,
    /// i.e. u.X + separation less or equal v.X
    /// </summary>
    public class HorizontalSeparationConstraint : IConstraint {
        bool equality;
        /// <summary>
        /// 
        /// </summary>
        public bool IsEquality => equality;
        private Node u;
        /// <summary>
        /// Constrained to be vertically above the BottomNode
        /// </summary>
        public Node LeftNode => u;
        private Node v;
        /// <summary>
        /// Constrained to be vertically below the TopNode
        /// </summary>
        public Node RightNode => v;
        private double separation;
        /// <summary>
        /// We allow the separation of existing constraints to be modified by the user.
        /// </summary>
        public double Separation { get => separation; set => separation = value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="u"></param>
        /// <param name="v"></param>
        /// <param name="separation"></param>
        public HorizontalSeparationConstraint(Node u, Node v, double separation)
        {
            this.u = u;
            this.v = v;
            this.separation = separation;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="u"></param>
        /// <param name="v"></param>
        /// <param name="separation"></param>
        /// <param name="equality"></param>
        public HorizontalSeparationConstraint(Node u, Node v, double separation, bool equality)
        {
            this.equality = equality;
            this.u = u;
            this.v = v;
            this.separation = separation;
        }
        /// <summary>
        /// 
        /// </summary>
        public virtual double Project() {
            double uv = v.Center.X - u.Center.X;
            double d = separation - uv;
            if (d > 0) {
                double
                    wu = ((FiNode)u.AlgorithmData).stayWeight,
                    wv = ((FiNode)v.AlgorithmData).stayWeight;
                double f = d / (wu + wv);
                u.Center = new Point(u.Center.X - wv * f, u.Center.Y);
                v.Center = new Point(v.Center.X + wu * f, v.Center.Y);
                return d;
            } else {
                return 0;
            }
        }
        /// <summary>
        /// HorizontalSeparationConstraint are usually structural and therefore default to level 0
        /// </summary>
        /// <returns>0</returns>
        public int Level => 0;
        /// <summary>
        /// Get the list of nodes involved in the constraint
        /// </summary>
        public IEnumerable<Node> Nodes => new Node[] { u, v };
    }
}