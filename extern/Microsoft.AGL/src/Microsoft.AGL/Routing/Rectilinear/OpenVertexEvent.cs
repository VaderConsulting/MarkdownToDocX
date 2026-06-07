//
// OpenVertexEvent.cs
// MSAGL Obstacle class for bottom-most (obstacle-opening) vertex events for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Rectilinear {
    internal class OpenVertexEvent: BasicVertexEvent {
        internal OpenVertexEvent(Obstacle obstacle, PolylinePoint p) : base(obstacle, p) { }
    }
}
