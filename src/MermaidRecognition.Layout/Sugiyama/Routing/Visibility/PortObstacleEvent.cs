using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    internal class PortObstacleEvent(Point site) : SweepEvent {
        readonly Point site = site;

        internal override Point Site => site;
    }
}