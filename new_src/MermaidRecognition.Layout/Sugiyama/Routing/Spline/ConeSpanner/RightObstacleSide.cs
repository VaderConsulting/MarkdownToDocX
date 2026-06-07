using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner
{
    internal class RightObstacleSide : ObstacleSide
    {
        Point end;
        internal RightObstacleSide(PolylinePoint startVertex)
            : base(startVertex)
        {
            this.end = startVertex.PrevOnPolyline.Point;
        }
        internal override Point End
        {
            get { return end; }
        }

        internal override PolylinePoint EndVertex
        {
            get { return StartVertex.PrevOnPolyline; }
        }

    }
}