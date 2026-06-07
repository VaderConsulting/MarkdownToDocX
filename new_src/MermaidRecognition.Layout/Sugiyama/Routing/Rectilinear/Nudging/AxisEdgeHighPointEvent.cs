using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    internal class AxisEdgeHighPointEvent : SweepEvent {
        Point site;
        internal AxisEdge AxisEdge { get; set; }

        public AxisEdgeHighPointEvent(AxisEdge edge, Point point) {
            site = point;
            AxisEdge = edge;
        }

        internal override Point Site {
            get { return site; }
        }


    }
}