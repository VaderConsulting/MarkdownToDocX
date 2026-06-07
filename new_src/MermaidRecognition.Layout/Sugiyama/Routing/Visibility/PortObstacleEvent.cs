using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    internal class PortObstacleEvent : SweepEvent {
        readonly Point site;

        public PortObstacleEvent(Point site) {
            this.site = site;
        }

        internal override Point Site {
            get { return site; }
        }
    }
}