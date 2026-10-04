using System.Collections.Generic;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    internal class StationEdgeInfo {
        internal StationEdgeInfo(Point iPos, Point jPos) {
            Point dir = jPos - iPos;
            double length = dir.Length;
            if (length > ApproximateComparer.DistanceEpsilon)
            {
                dir /= length;
            }
        }

        internal int Count;
        internal double Width;

        internal List<Metroline> Metrolines = [];

        #region cache

        internal double cachedBundleCost;

        //internal Polyline cachedBoundary;

        #endregion
    }
}