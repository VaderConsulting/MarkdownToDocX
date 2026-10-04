using System.Collections.Generic;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    internal class StationEdgeInfo {
        internal StationEdgeInfo(Point iPos, Point jPos) {
            Point dir = jPos - iPos;
            double length = dir.Length;
            if (length > ApproximateComparer.DistanceEpsilon)
                dir /= length;
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