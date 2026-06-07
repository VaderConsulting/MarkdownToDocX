//
// OpenVertexEvent.cs
// MSAGL Obstacle class for bottom-most (obstacle-opening) vertex events for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal class OpenVertexEvent: BasicVertexEvent {
        internal OpenVertexEvent(Obstacle obstacle, PolylinePoint p) : base(obstacle, p) { }
    }
}
