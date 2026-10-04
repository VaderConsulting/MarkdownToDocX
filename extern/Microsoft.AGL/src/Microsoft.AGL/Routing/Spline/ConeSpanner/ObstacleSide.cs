using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Routing.Visibility;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner
{
    internal abstract class ObstacleSide : SegmentBase
    {
        internal PolylinePoint StartVertex { get; private set; }
        internal ObstacleSide(PolylinePoint startVertex) => StartVertex = startVertex;

        internal abstract PolylinePoint EndVertex { get; }

        internal Polyline Polyline => StartVertex.Polyline;

        internal override Point Start => StartVertex.Point;

        internal override Point End => EndVertex.Point;

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            string typeString = GetType().ToString();
            int lastDotLoc = typeString.LastIndexOf('.');
            if (lastDotLoc >= 0) {
                typeString = typeString.Substring(lastDotLoc + 1);
            }
            return typeString + " [" + Start.ToString() + " -> " + End.ToString() + "]";
        }
    }
}
