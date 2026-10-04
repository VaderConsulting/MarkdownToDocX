using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    /// <summary>
    /// left here means an intersection of a left cone side with an obstacle edge
    /// </summary>
    internal class LeftIntersectionEvent : SweepEvent {
        internal ConeLeftSide coneLeftSide;
        Point intersectionPoint;
        PolylinePoint endVertex;

        internal PolylinePoint EndVertex => endVertex;

        internal LeftIntersectionEvent(ConeLeftSide coneLeftSide,
            Point intersectionPoint,
            PolylinePoint endVertex) {
            this.coneLeftSide = coneLeftSide;
            this.intersectionPoint = intersectionPoint;
            this.endVertex = endVertex;
        }

        internal override Point Site => intersectionPoint;

        public override string ToString() => "LeftIntersectionEvent " + intersectionPoint;
    }
}
