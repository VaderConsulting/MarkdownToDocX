using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// Same behavior as RelativeFloatingPort but layout engines can choose the best relative
    /// location for routing from the list of possible locations.
    /// </summary>
    /// <remarks>
    /// Same behavior as RelativeFloatingPort but layout engines can choose the best offset for routing
    /// from the list of possible offsets.
    /// </remarks>
    /// <param name="curveDelegate">The curve the locations are relative to</param>
    /// <param name="centerDelegate">The center the locations are relative to</param>
    /// <param name="possibleOffsets">The offsets from the center that form the locations</param>
    public class MultiLocationFloatingPort(Func<ICurve> curveDelegate,
            Func<Point> centerDelegate, List<Point> possibleOffsets) : RelativeFloatingPort(curveDelegate, centerDelegate) {
         List<Point> PossibleOffsets = possibleOffsets;

        /// <summary>
        /// Enumerate the offsets this was created with
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Point> LocationOffsets => PossibleOffsets;

        /// <summary>
        /// LocationOffset is PossibleOffsets[ActiveOffsetIndex]
        /// </summary>
        public int ActiveOffsetIndex { get; private set; }

        /// <summary>
        /// LocationOffset is PossibleOffsets[ActiveOffsetIndex]
        /// </summary>
        public override Point LocationOffset             // The set() is private to RelativeFloatingPort; this class should set ActiveOffsetIndex
                                                         // rather than setting LocationOffset directly.
            => PossibleOffsets[ActiveOffsetIndex];

        /// <summary>
        /// Set LocationOffset to the PossibleOffset + CenterDelegate() that is closest to point.
        /// </summary>
        /// <param name="point"></param>
        internal void SetClosestLocation(Point point) {
            // Set the ActiveOffsetIndex to the first offset initially; this indirectly sets
            // Location via LocationOffset.
            ActiveOffsetIndex = 0;
            double mind = (point - Location).LengthSquared;

            // If there's another offset that's closer to the point, update ActiveOffsetIndex.
            int n = PossibleOffsets.Count;
            for (int i = 1; i < n; ++i) {
                Point p = PossibleOffsets[i] + CenterDelegate();
                double d = (point - p).LengthSquared;
                if (d < mind) {
                    mind = d;
                    ActiveOffsetIndex = i;
                }
            }
        }


        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(CenterDelegate());
            builder.Append(" [");
            bool first = false;
            foreach (Point p in LocationOffsets) {
                if (first) {
                    builder.Append(", ");
                    first = false;
                }
                builder.Append(p.ToString());
            }
            builder.Append("]");
            return builder.ToString();
        }
    }
}
