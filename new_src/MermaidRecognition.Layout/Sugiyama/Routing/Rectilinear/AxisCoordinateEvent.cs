//
// AxisCoordinateEvent.cs
// MSAGL class for axis-coordinate events for sparse VisibilityGraph generation for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
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
