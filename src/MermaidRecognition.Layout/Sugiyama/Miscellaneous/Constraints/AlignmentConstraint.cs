using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Layout.Incremental;

namespace MermaidRecognition.Layout.Sugiyama.Miscellaneous.Constraints
{
    /// <summary>
    /// This doesn't work yet, but it will be a constraint that aligns nodes, optionally to some fixed orientation (horizontal, vertical, 45 degrees, etc).
    /// </summary>
    /// <remarks>
    /// Create an alignment constraint for the given set of nodes
    /// </remarks>
    /// <param name="nodes"></param>
    public class AlignmentConstraint(IEnumerable<Node> nodes) : IConstraint {
        List<Node> nodes = [.. nodes];

        void leastSquaresLineFit(out double a, out double b) {
            double mx = 0, my = 0, sx2 = 0, sy2 = 0, sxy=0, n = nodes.Count;
            foreach (Node v in nodes) {
                double x = v.Center.X, y = v.Center.Y;
                mx += x;
                my += y;
                sx2 += x * x;
                sy2 += y * y;
                sxy += x * y;
            }
            mx /= n;
            my /= n;
            double B = 0.5*((sy2 - (n * my * my)) - (sx2 - (n * mx * mx))) / ((n * mx * my) - sxy);
            b = Math.Sqrt((B * B) + 1) - B;
            //b = - Math.Sqrt(B * B + 1) - B;
            a = my - (b * mx);
        }

        #region IConstraint Members
        /// <summary>
        /// project using a perpendicular least-squares fit
        /// </summary>
        /// <returns></returns>
        public double Project() {
            double d = 0;
            leastSquaresLineFit(out double a, out double b);
            // project to the line y = a + b x
            foreach (Node v in nodes) {
                double x0 = v.Center.X, y0 = v.Center.Y;
                double x = ((b * y0) + x0 ) / ((b * b) + 1.0);
                double y = a + (b * x);
                Point p = new Point(x, y);
                d = (v.Center - p).Length;
                v.Center = p;
            }
            return d;
        }
        /// <summary>
        /// level 1
        /// </summary>
        public int Level => 1;
        /// <summary>
        /// Get the list of nodes involved in the constraint
        /// </summary>
        public IEnumerable<Node> Nodes => nodes;

        #endregion
    }
}