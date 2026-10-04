using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner {
    internal class PortLocationEvent(Point portLocation) : SweepEvent {
        internal override Point Site => PortLocation;

        protected Point PortLocation { get; set; } = portLocation;
    }
}