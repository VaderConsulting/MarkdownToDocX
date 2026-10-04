using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal class LeftObstacleSide : ObstacleSide {
        readonly Point end;
        internal LeftObstacleSide(PolylinePoint startVertex)
            : base(startVertex) => end = startVertex.NextOnPolyline.Point;

        internal override Point End => end;

        internal override PolylinePoint EndVertex => StartVertex.NextOnPolyline;

    }
}
