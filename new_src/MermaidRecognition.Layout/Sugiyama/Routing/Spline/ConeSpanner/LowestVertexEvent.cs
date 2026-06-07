using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner{
    internal class LowestVertexEvent:VertexEvent {
        internal 
            LowestVertexEvent(PolylinePoint p) : base(p) { }
    }
}
