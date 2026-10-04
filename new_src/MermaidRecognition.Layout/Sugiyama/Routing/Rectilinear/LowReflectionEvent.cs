//
// LowReflectionEvent.cs
// MSAGL Obstacle class for reflection events from a LowObstacleSide for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    /// <summary>
    /// This records an intersection of a lookahead ray with an obstacle edge.
    /// </summary>
    internal class LowReflectionEvent : BasicReflectionEvent {
        internal LowObstacleSide Side { get; private set; }

        internal LowReflectionEvent(BasicReflectionEvent previousSite, LowObstacleSide targetSide, Point site)
            : base(previousSite, targetSide.Obstacle, site) => Side = targetSide;
    }
}
