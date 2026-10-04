using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner
{
    internal class RightObstacleSide : ObstacleSide
    {
        Point end;
        internal RightObstacleSide(PolylinePoint startVertex)
            : base(startVertex) => end = startVertex.PrevOnPolyline.Point;
        internal override Point End => end;

        internal override PolylinePoint EndVertex => StartVertex.PrevOnPolyline;

    }
}