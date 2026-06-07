//
// HighBendVertexEvent.cs
// MSAGL Obstacle class for vertex events on a HighObstacleSide for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal class HighBendVertexEvent: BasicVertexEvent {
        internal HighBendVertexEvent(Obstacle obstacle, PolylinePoint p) : base(obstacle, p) { }
    }
}
