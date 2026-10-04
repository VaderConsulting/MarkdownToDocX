//
// ObstaclePort.cs
// MSAGL class for ObstaclePorts for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System.Collections.Generic;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal class ObstaclePort {
        internal Port Port { get; private set; }
        internal Obstacle Obstacle { get; private set; }

        internal VisibilityVertex CenterVertex;

        // These are derived from PortEntry spans if present, else from Port.Location.
        internal List<ObstaclePortEntrance> PortEntrances { get; private set; }

        internal bool HasCollinearEntrances { get; private set; }

        // Hang onto this separately to detect port movement.
        internal Point Location { get; private set; }

        internal Rectangle VisibilityRectangle = Rectangle.CreateAnEmptyBox();

        internal ObstaclePort(Port port, Obstacle obstacle) {
            Port = port;
            Obstacle = obstacle;
            PortEntrances = [];
            Location = ApproximateComparer.Round(Port.Location);
        }

        internal void CreatePortEntrance(Point unpaddedBorderIntersect, Directions outDir, ObstacleTree obstacleTree) {
            ObstaclePortEntrance entrance = new ObstaclePortEntrance(this, unpaddedBorderIntersect, outDir, obstacleTree);
            PortEntrances.Add(entrance);
            VisibilityRectangle.Add(entrance.MaxVisibilitySegment.End);
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=370
            this.HasCollinearEntrances = this.HasCollinearEntrances | entrance.IsCollinearWithPort;
#else
            HasCollinearEntrances |= entrance.IsCollinearWithPort;
#endif
        }

        internal void Clear() {
            RemoveFromGraph();
            PortEntrances.Clear();
        }

        internal void ClearVisibility() =>
            // Most of the retained PortEntrance stuff is about precalculated visibility.
            PortEntrances.Clear();

        internal void AddToGraph(TransientGraphUtility transUtil, bool routeToCenter) {
            // We use only border vertices if !routeToCenter.
            if (routeToCenter) {
                CenterVertex = transUtil.FindOrAddVertex(Location);
            }
        }

        internal void RemoveFromGraph() {
            // Currently all transient removals and edge restorations are done by TransientGraphUtility itself.
            foreach (ObstaclePortEntrance entrance in PortEntrances) {
                entrance.RemoveFromGraph();
            }
            CenterVertex = null;
        }

        // PortManager will recreate the Port if it detects this (this.Location has already been rounded).
        internal bool LocationHasChanged => !PointComparer.Equal(Location, ApproximateComparer.Round(Port.Location));

        /// <summary>
        /// The curve associated with the port.
        /// </summary>
        public ICurve PortCurve => Port.Curve;

        /// <summary>
        /// The (unrounded) location of the port.
        /// </summary>
        public Point PortLocation => Port.Location;

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Port + Obstacle.ToString();
    }
}