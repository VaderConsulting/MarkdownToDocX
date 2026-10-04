using System;
using System.Collections.Generic;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.Incremental {
    /// <summary>
    /// A stick constraint requires a fixed separation between two nodes
    /// </summary>
    public class StickConstraint : IConstraint {
        /// <summary>
        /// 
        /// </summary>
        protected Node u;

        /// <summary>
        /// 
        /// </summary>
        protected Node v;

        /// <summary>
        /// 
        /// </summary>
        protected double separation;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="u"></param>
        /// <param name="v"></param>
        /// <param name="separation"></param>
        public StickConstraint(Node u, Node v, double separation) {
            this.u = u;
            this.v = v;
            this.separation = separation;
        }
        /// <summary>
        /// 
        /// </summary>
        public virtual double Project() {
            Point uv = v.Center - u.Center;
            double d = separation - uv.Length,
                   wu = ((FiNode)u.AlgorithmData).stayWeight,
                   wv = ((FiNode)v.AlgorithmData).stayWeight;
            Point f = d * uv.Normalize() / (wu + wv);
            u.Center -= wv * f;
            v.Center += wu * f;
            return Math.Abs(d);
        }
        /// <summary>
        /// StickConstraints are usually structural and therefore default to level 0
        /// </summary>
        /// <returns>0</returns>
        public int Level => 0;

        /// <summary>
        /// Get the list of nodes involved in the constraint
        /// </summary>
        public IEnumerable<Node> Nodes => new Node[] { u, v };
    }
}