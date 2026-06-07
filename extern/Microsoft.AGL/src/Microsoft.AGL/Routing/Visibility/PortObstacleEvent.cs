using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Spline.ConeSpanner;

namespace Microsoft.AGL.Routing.Visibility {
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