//
// LowBendVertexEvent.cs
// MSAGL Obstacle class for vertex events on a LowObstacleSide for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal class LowBendVertexEvent: BasicVertexEvent {
        internal LowBendVertexEvent(Obstacle obstacle, PolylinePoint p) : base(obstacle, p) { }
    }
}
