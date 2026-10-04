using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner {
    internal class PortLocationEvent : SweepEvent {
        public PortLocationEvent(Point portLocation) => PortLocation = portLocation;

        internal override Point Site => PortLocation;

        protected Point PortLocation { get; set; }
    }
}