using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Spline.ConeSpanner;

namespace Microsoft.AGL.Routing.Rectilinear.Nudging {
    internal class AxisEdgeHighPointEvent : SweepEvent {
        Point site;
        internal AxisEdge AxisEdge { get; set; }

        public AxisEdgeHighPointEvent(AxisEdge edge, Point point) {
            site = point;
            AxisEdge = edge;
        }

        internal override Point Site => site;


    }
}