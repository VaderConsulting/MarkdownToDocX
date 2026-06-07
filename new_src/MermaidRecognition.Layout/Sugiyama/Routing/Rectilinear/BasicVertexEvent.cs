//
// BasicVertexEvent.cs
// MSAGL Base class for vertex events for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal abstract class BasicVertexEvent : VertexEvent {
        // This is just a subclass to carry the Obstacle object in addition to the Polyline.
        internal Obstacle Obstacle { get; private set; }

        internal BasicVertexEvent(Obstacle obstacle, PolylinePoint p) : base (p) {
            this.Obstacle = obstacle;
        }
    }
}
