using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Incremental {
    /// <summary>
    /// Keeps nodes within a specified distance of each other
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="u"></param>
    /// <param name="v"></param>
    /// <param name="separation"></param>
    public class MaxSeparationConstraint(Node u, Node v, double separation) : StickConstraint(u, v, separation) {
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