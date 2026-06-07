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

        internal Metroline Metroline {
            get { return metroline; }
        }

        internal PolylinePoint PolyPoint {
            get { return polyPoint; }
        }

        internal Station Station {
            get { return station; }
        }
    }
}