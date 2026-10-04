using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    internal class AxisEdgeLowPointEvent : SweepEvent {
        Point site;
        
        internal AxisEdge AxisEdge { get; set; }

        public AxisEdgeLowPointEvent(AxisEdge  edge, Point point) {
            site = point;
            AxisEdge = edge;
        }

        internal override Point Site => site;


    }
}