using System;
using System.Diagnostics;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Routing.Visibility;

namespace Microsoft.AGL.Layout.LargeGraphLayout {


    /// <summary>
    /// keeps a part of EdgeGeometry which is visible
    /// </summary>
    public class Rail {

#if DEBUG
        static int railCount;
        int id;
#endif

        /// <summary>
        /// the number of higlighted edges passing through the rail
        /// </summary>
        bool _isHighlighted;

        /// <summary>
        /// can be ICurve or Arrowhead
        /// </summary>
        public object Geometry;

        /// <summary>
        /// the point where the edge curve touches the arrowhead
        /// </summary>
        public Point CurveAttachmentPoint;

        /// <summary>
        /// the corresponding LgEdgeInfo
        /// </summary>
        public LgEdgeInfo TopRankedEdgeInfoOfTheRail;

        bool _isUsedOnPreviousLevel;

        public double MinPassingEdgeZoomLevel = double.MaxValue;

        public bool IsUsedOnPreviousLevel
        { get => _isUsedOnPreviousLevel; set => _isUsedOnPreviousLevel = value;
        }

        internal int ZoomLevel;
#if DEBUG
        Rail() {
            railCount++;
            id = railCount;
        }
        /// <summary>
        /// returning id for debugging
        /// </summary>
        /// <returns></returns>
        public int GetId() => id;
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString() => id + StartEndString();

        string StartEndString() => GetStartEnd(out Point s, out Point t) ? " " + new LineSegment(s, t) : "";
#endif

        internal Rail(ICurve curveSegment, LgEdgeInfo topRankedEdgeInfoOfTheRail, int zoomLevel)
#if DEBUG
            : this()
#endif
        {
            TopRankedEdgeInfoOfTheRail = topRankedEdgeInfoOfTheRail;
            ZoomLevel = zoomLevel;
            Geometry = curveSegment;
        }

        internal Rail(Arrowhead arrowhead, Point curveAttachmentPoint, LgEdgeInfo topRankedEdgeInfoOfTheRail,
            int zoomLevel)
#if DEBUG
            : this()
#endif
        {
            TopRankedEdgeInfoOfTheRail = topRankedEdgeInfoOfTheRail;
            Geometry = arrowhead.Clone();
            CurveAttachmentPoint = curveAttachmentPoint;
            ZoomLevel = zoomLevel;
        }

        public Rectangle BoundingBox {
            get {
                if (Geometry is ICurve icurve)
                {
                    return icurve.BoundingBox;
                }

                Arrowhead arrowhead = (Arrowhead) Geometry;
                Rectangle rec = new Rectangle(arrowhead.TipPosition, CurveAttachmentPoint);
                rec.Pad(arrowhead.Width); // sometimes this box will not cover the arrowhead, but rarely
                return rec;
            }
        }

        /// <summary>
        /// the number of higlighted edges passing through the rail
        /// </summary>
        public bool IsHighlighted { get => _isHighlighted; set => _isHighlighted = value;
        }



        internal Tuple<Point, Point> PointTuple() {
            if (Geometry is ICurve icurve)
            {
                return new Tuple<Point, Point>(icurve.Start, icurve.End);
            }

            Arrowhead arrowhead = (Arrowhead) Geometry;
            return new Tuple<Point, Point>(arrowhead.TipPosition, CurveAttachmentPoint);
        }

        
        public Rail(ICurve curveSegment, int zoomLevel)
        {
            ZoomLevel = zoomLevel;
#if DEBUG
            railCount++;
            id = railCount;
#endif
            Geometry = curveSegment;
        }

        public bool GetStartEnd(out Point p0, out Point p1) {
            if (Geometry is ICurve curve)
            {
                p0 = curve.Start;
                p1 = curve.End;
                return true;
            }
            if (Geometry is Arrowhead arrow)
            {
                p0 = CurveAttachmentPoint;
                p1 = arrow.TipPosition;
                return true;
            }
            p0 = new Point();
            p1 = new Point();
            return false;
        }

        public void UpdateTopEdgeInfo(LgEdgeInfo ei)
        {
            if (TopRankedEdgeInfoOfTheRail == null || TopRankedEdgeInfoOfTheRail.Rank < ei.Rank)
            {
                TopRankedEdgeInfoOfTheRail = ei;
            }

            MinPassingEdgeZoomLevel = Math.Min(MinPassingEdgeZoomLevel, ei.ZoomLevel);
        }

        public LgEdgeInfo GetTopEdgeInfo() => TopRankedEdgeInfoOfTheRail;
    }
}