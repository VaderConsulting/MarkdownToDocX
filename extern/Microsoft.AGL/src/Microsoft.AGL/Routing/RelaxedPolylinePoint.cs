using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing {
    internal class RelaxedPolylinePoint  {
        private PolylinePoint polylinePoint;

        internal PolylinePoint PolylinePoint { get => polylinePoint; set => polylinePoint = value;
        }
        private Point originalPosition;

        internal Point OriginalPosition { get => originalPosition; set => originalPosition = value;
        }

        internal RelaxedPolylinePoint(PolylinePoint polylinePoint, Point originalPosition) {
            PolylinePoint = polylinePoint;
            OriginalPosition = originalPosition;
        }

        RelaxedPolylinePoint next;

        internal RelaxedPolylinePoint Next { get => next; set => next = value;
        }
        RelaxedPolylinePoint prev;

        internal RelaxedPolylinePoint Prev { get => prev; set => prev = value;
        }
    }
}
