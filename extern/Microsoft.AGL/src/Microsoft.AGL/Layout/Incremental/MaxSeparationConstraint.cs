using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.Incremental {
    /// <summary>
    /// Keeps nodes within a specified distance of each other
    /// </summary>
    public class MaxSeparationConstraint : StickConstraint {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="u"></param>
        /// <param name="v"></param>
        /// <param name="separation"></param>
        public MaxSeparationConstraint(Node u, Node v, double separation)
            : base(u, v, separation) { }
        /// <summary>
        /// 
        /// </summary>
        public override double Project() {
            Point uv = v.Center - u.Center;
            if (uv.Length > separation) {
                return base.Project();
            } else {
                return 0;
            }
        }
    }
}