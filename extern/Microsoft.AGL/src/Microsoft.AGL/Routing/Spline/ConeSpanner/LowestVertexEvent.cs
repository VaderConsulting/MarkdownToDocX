using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner{
    internal class LowestVertexEvent:VertexEvent {
        internal 
            LowestVertexEvent(PolylinePoint p) : base(p) { }
    }
}
