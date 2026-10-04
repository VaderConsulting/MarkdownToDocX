using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner {
    internal abstract class VertexEvent: SweepEvent {
        PolylinePoint vertex;

        internal PolylinePoint Vertex { get => vertex; set => vertex = value;
        }

        internal override Point Site => vertex.Point;

        internal VertexEvent(PolylinePoint p) => vertex = p;

        internal Polyline Polyline => vertex.Polyline;
    }
}
