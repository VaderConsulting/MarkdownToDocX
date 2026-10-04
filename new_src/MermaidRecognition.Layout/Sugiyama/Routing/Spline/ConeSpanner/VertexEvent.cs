using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    abstract internal class VertexEvent: SweepEvent {
        PolylinePoint vertex;

        internal PolylinePoint Vertex { get => vertex; set => vertex = value;
        }

        internal override Point Site => vertex.Point;

        internal VertexEvent(PolylinePoint p) => vertex = p;
        internal Polyline Polyline => vertex.Polyline;
    }
}
