using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Spline.ConeSpanner;

namespace Microsoft.AGL.Routing.Visibility {
    internal class PortObstacleEvent(Point site) : SweepEvent {
        readonly Point site = site;

        internal override Point Site => site;
    }
}