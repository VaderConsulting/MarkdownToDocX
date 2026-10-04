//
// BasicReflectionEvent.cs
// MSAGL Base class for reflection events for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal class BasicReflectionEvent : SweepEvent {
        internal Obstacle ReflectingObstacle { get; private set; }
        internal Obstacle InitialObstacle { get; private set; }
        internal BasicReflectionEvent PreviousSite { get; private set; }

        // Called by StoreLookaheadSite only.
        internal BasicReflectionEvent(Obstacle initialObstacle, Obstacle reflectingObstacle, Point site) {
            InitialObstacle = initialObstacle;
            ReflectingObstacle = reflectingObstacle;
            this.site = site;
        }

        // Called by LowReflectionEvent or HighReflectionEvent ctors, which are called out of 
        // AddReflectionEvent, which in turn is called by LoadLookaheadIntersections.
        // In this case we know the eventObstacle and initialObstacle are the same obstacle (the
        // one that the reflected ray bounced off of, to generate the Left/HighReflectionEvent).
        internal BasicReflectionEvent(BasicReflectionEvent previousSite, Obstacle reflectingObstacle, Point site) {
            InitialObstacle = previousSite.ReflectingObstacle;
            ReflectingObstacle = reflectingObstacle;
            this.site = site;
            PreviousSite = previousSite;
        }

        // If true, we have a staircase situation.
        internal bool IsStaircaseStep(Obstacle reflectionTarget) => (InitialObstacle == reflectionTarget);

        private readonly Point site;
        internal override Point Site => site;
    }
}
