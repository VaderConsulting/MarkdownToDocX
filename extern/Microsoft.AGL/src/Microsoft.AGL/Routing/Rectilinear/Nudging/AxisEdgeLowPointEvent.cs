using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Spline.ConeSpanner;

namespace Microsoft.AGL.Routing.Rectilinear.Nudging {
    internal class AxisEdgeLowPointEvent(AxisEdge edge, Point point) : SweepEvent {
        Point site = point;

        internal AxisEdge AxisEdge { get; set; } = edge;

        internal override Point Site => site;


    }
}