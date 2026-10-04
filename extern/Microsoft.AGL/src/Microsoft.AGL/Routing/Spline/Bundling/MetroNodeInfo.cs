using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    internal class MetroNodeInfo {
        Metroline metroline;
        Station station;
        PolylinePoint polyPoint;

        internal MetroNodeInfo(Metroline metroline, Station station, PolylinePoint polyPoint) {
            this.metroline = metroline;
            this.station = station;
            this.polyPoint = polyPoint;
        }

        internal Metroline Metroline => metroline;

        internal PolylinePoint PolyPoint => polyPoint;

        internal Station Station => station;
    }
}