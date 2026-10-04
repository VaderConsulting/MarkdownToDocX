using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    internal class AxisEdgeLowPointEvent(AxisEdge edge, Point point) : SweepEvent {
        Point site = point;

        internal AxisEdge AxisEdge { get; set; } = edge;

        internal override Point Site => site;


    }
}