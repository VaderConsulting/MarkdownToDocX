using System;
using System.Globalization;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves {
    /// <summary>
    /// Contains the result of the intersection of two ICurves.
    /// </summary>
    public class IntersectionInfo {


        /* The following conditions should hold:
         * X=seg0[par0]=seg1[par1]
         */ 

        double par0, par1;
        Point x;

        ICurve seg0, seg1;
        /// <summary>
        /// the parameter on the first curve
        /// </summary>
        public double Par0 { get => par0; set => par0 = value;
        }
        /// <summary>
        /// the parameter on the second curve
        /// </summary>
        public double Par1 { get => par1; set => par1 = value;
        }

        /// <summary>
        /// the intersection point
        /// </summary>
        public Point IntersectionPoint { get => x; set => x = value;
        }

        /// <summary>
        /// the segment of the first curve where the intersection point belongs
        /// </summary>
        public ICurve Segment0 { get => seg0; set => seg0 = value;
        }

        /// <summary>
        /// the segment of the second curve where the intersection point belongs
        /// </summary>
        public ICurve Segment1 { get => seg1; set => seg1 = value;
        }

        /// <summary>
        /// the constructor
        /// </summary>
        /// <param name="pr0"></param>
        /// <param name="pr1"></param>
        /// <param name="x"></param>
        /// <param name="s0"></param>
        /// <param name="s1"></param>
        internal IntersectionInfo(double pr0, double pr1, Point x, ICurve s0, ICurve s1) {
            par0 = pr0;
            par1 = pr1;
            this.x = x;
            seg0 = s0;
            seg1 = s1;
#if DETAILED_DEBUG
            System.Diagnostics.Debug.Assert(ApproximateComparer.Close(x, s0[pr0], ApproximateComparer.IntersectionEpsilon*10),
                    string.Format("intersection not at curve[param]; x = {0}, s0[pr0] = {1}, diff = {2}", x, s0[pr0], x - s0[pr0]));
            System.Diagnostics.Debug.Assert(ApproximateComparer.Close(x, s1[pr1], ApproximateComparer.IntersectionEpsilon*10),
                    string.Format("intersection not at curve[param]; x = {0}, s1[pr1] = {1}, diff = {2}", x, s1[pr1], x - s1[pr1]));
#endif 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "XX({0} {1} {2})", par0, par1, x);
    }
}
