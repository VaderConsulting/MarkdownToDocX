using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    abstract internal class VertexEvent: SweepEvent {
        PolylinePoint vertex;

        internal PolylinePoint Vertex {
            get { return vertex; }
            set { vertex = value; }
        }

        internal override Point Site {
            get { return vertex.Point; }
        }

        internal VertexEvent(PolylinePoint p) { vertex = p; }
        internal Polyline Polyline { get { return vertex.Polyline; } }
    }
}
