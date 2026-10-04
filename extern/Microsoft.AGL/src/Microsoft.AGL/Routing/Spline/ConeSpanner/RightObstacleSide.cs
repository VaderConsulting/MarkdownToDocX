using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner
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