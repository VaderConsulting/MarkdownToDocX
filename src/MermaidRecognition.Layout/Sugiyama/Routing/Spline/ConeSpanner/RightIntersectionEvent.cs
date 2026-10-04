using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    /// <summary>
    /// right here means an intersection of a right cone side with an obstacle edge
    /// </summary>
    internal class RightIntersectionEvent: SweepEvent {
        internal ConeRightSide coneRightSide;
        Point intersectionPoint;
        PolylinePoint endVertex;

        internal PolylinePoint EndVertex { get => endVertex; set => endVertex = value;
        }

        internal RightIntersectionEvent(ConeRightSide coneRightSide,
            Point intersectionPoint, PolylinePoint endVertex) {
            this.coneRightSide = coneRightSide;
            this.intersectionPoint = intersectionPoint;
            this.endVertex = endVertex;
        }

        internal override Point Site => intersectionPoint;

        public override string ToString() => "RightIntersectionEvent " + intersectionPoint;
    }
}
