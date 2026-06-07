using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner {
    internal class LeftObstacleSide : ObstacleSide {
        readonly Point end;
        internal LeftObstacleSide(PolylinePoint startVertex)
            : base(startVertex) {
            end = startVertex.NextOnPolyline.Point;
        }
        internal override Point End {
            get { return end; }
        }

        internal override PolylinePoint EndVertex {
            get { return StartVertex.NextOnPolyline; }
        }

    }
}
