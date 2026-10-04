using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing {
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
