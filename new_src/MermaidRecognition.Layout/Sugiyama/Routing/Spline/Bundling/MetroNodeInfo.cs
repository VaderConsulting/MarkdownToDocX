using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
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