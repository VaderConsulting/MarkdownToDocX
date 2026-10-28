using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
#if TEST_MSAGL
using System.Diagnostics;
#endif

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout {
    ///<summary>
    ///</summary>
    public class PortEntryOnCurve : IPortEntry {
        Rectangle[] allowedRects;
        /// <summary>
        /// returns an enumeration of the rectangles that are allowed for routing
        /// </summary>
        public IEnumerable<Rectangle> AllowedRectangles {
            
            get {
                if (allowedRects == null) {
                    allowedRects = [.. Spans.Select(span => TrimEntryCurve(span).BoundingBox)];
                }
                return allowedRects;
            }
        }

         ICurve TrimEntryCurve(Tuple<double, double> span) {
            double start = span.Item1;
            double end = span.Item2;
            if (start < end) {
                return EntryCurve.Trim(start, end);
            }
            
            // For the classes that implement it, wrap the Trim.
            if ((EntryCurve is Polyline) || (EntryCurve is Curve) || (EntryCurve is RoundedRect)) {
                return EntryCurve.TrimWithWrap(start, end);
            }

            // Ellipse does not (yet) support TrimWithWrap but for our purposes we can deal with it as a Curve.
            if (EntryCurve is Ellipse) {
                Curve c = new Curve();
                c.AddSegment(EntryCurve.Trim(start, EntryCurve.ParEnd));
                c.AddSegment(EntryCurve.Trim(EntryCurve.ParStart, end));
                return c;
            }

            // For the remaining implementations of ICurve, this is what Trim does if start is greater than end, unless it throws.
            return EntryCurve.Trim(end, start);
        }

        /// <summary>
        /// paremeter spans
        /// </summary>
        public IEnumerable<Tuple<double, double>> Spans { get; private set; }
#if TEST_MSAGL
        readonly bool curveIsClosed;
#endif
        ///<summary>
        ///</summary>
        ///<param name="entryCurve"></param>
        ///<param name="parameterSpans"></param>
        public PortEntryOnCurve(ICurve entryCurve, IEnumerable<Tuple<double, double>> parameterSpans) {
            EntryCurve = entryCurve;
#if TEST_MSAGL
            curveIsClosed = (entryCurve is Polyline polyline) ? polyline.Closed : ApproximateComparer.Close(EntryCurve.Start, EntryCurve.End);
#endif
            Spans = parameterSpans;
#if TEST_MSAGL
            TestSpans(entryCurve, parameterSpans, curveIsClosed);
#endif
        }

#if TEST_MSAGL
        static void TestSpans(ICurve entryCurve, IEnumerable<Tuple<double, double>> parameterSpans, bool curveClosed) {
            foreach (Tuple<double, double> parameterSpan in parameterSpans)
            {
                Debug.Assert(InParamDomain(entryCurve, parameterSpan, curveClosed));
            }
        }

        static bool InParamDomain(ICurve entryCurve, Tuple<double, double> parameterSpan, bool curveClosed) => InParamDomain(entryCurve, parameterSpan.Item1) && InParamDomain(entryCurve, parameterSpan.Item2) &&
                   (parameterSpan.Item1 <= parameterSpan.Item2 || curveClosed);

        static bool InParamDomain(ICurve entryCurve, double parameter) => entryCurve.ParStart <= parameter && entryCurve.ParEnd >= parameter;
#endif
        internal ICurve EntryCurve { get; set; }

        /// <summary>
        /// returns the points uniformly distributed over the entries
        /// </summary>
        /// <returns></returns>      
        public IEnumerable<Point> GetEntryPoints() => Spans.SelectMany(SpanPoints);

        IEnumerable<Point> SpanPoints(Tuple<double, double> span) {
            if ((span.Item1 == EntryCurve.ParStart && span.Item2 == EntryCurve.ParEnd) ||
                (span.Item2 == EntryCurve.ParStart && span.Item1 == EntryCurve.ParEnd))
            {
                return [MiddlePoint(EntryCurve)];
            }

            if (EntryCurve is Polyline poly)
            {
                return SpanPointsFromPolyline(poly, span);
            }

            // If the EntryCurve is of a type that may contain multiple segments, get their midpoints.
            if ((EntryCurve is Polyline) || (EntryCurve is Curve) || (EntryCurve is RoundedRect)) {
                if (TrimEntryCurve(span) is Curve trimmedCurve)
                {
                    return SpanPointsFromCurveSegments(trimmedCurve);
                }
            }

            // Otherwise we just take the midpoint of the span.  Ellipse could be made smart enough here to
            // get a midpoint for each axis it faces.
            return [EntryCurve[MiddleOfSpan(span)]];
        }

        static Point MiddlePoint(ICurve c) => c[(c.ParStart + c.ParEnd) / 2];

        double MiddleOfSpan(Tuple<double, double> span) {
            if (span.Item1 < span.Item2)
            {
                return 0.5*(span.Item1 + span.Item2);
            }

            double halfLen = GetSpanLength(span)/2;
            double t = span.Item1 + halfLen;
            return t <= EntryCurve.ParEnd ? t : t - EntryCurve.ParStart;
        }

        static IEnumerable<Point> SpanPointsFromCurveSegments(Curve curve) => curve.Segments.Select(MiddlePoint);

        static IEnumerable<Point> SpanPointsFromPolyline(Polyline poly, Tuple<double, double> span) {
            Polyline trimmedPoly = (Polyline)poly.TrimWithWrap(span.Item1, span.Item2);
            for (PolylinePoint p = trimmedPoly.StartPoint; p.Next != null; p = p.Next)
            {
                yield return (p.Point + p.Next.Point)/2;
            }
        }

        double GetSpanLength(Tuple<double, double> span) => span.Item1 < span.Item2 ? span.Item2 - span.Item1 : EntryCurve.ParEnd - span.Item1 + span.Item2;
    }
}