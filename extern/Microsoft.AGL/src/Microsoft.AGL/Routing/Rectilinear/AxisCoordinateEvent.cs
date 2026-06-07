//
// AxisCoordinateEvent.cs
// MSAGL class for axis-coordinate events for sparse VisibilityGraph generation for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Spline.ConeSpanner;

namespace Microsoft.AGL.Routing.Rectilinear {
    internal class AxisCoordinateEvent : SweepEvent {
        internal AxisCoordinateEvent(Point p) {
            this.site = p;
        }

        internal override Point Site {
            get { return site; }
        }

        private readonly Point site;
    }
}
