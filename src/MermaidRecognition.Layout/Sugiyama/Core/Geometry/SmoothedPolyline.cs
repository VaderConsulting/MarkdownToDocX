using System;
using System.Collections.Generic;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry {
    /// <summary>
    /// represents the polyline of an edge
    /// </summary>
    /// <remarks>
    /// a constructor
    /// </remarks>
    /// <param name="head"></param>
    [

#if TEST_MSAGL
Serializable
#endif
    ]
    public class SmoothedPolyline(Site head) : IEnumerable<Point> {


        /// <summary>
        /// creates the polyline from corner points
        /// </summary>
        ///<param name="points">points of the polyline</param>
        public static SmoothedPolyline FromPoints(IEnumerable<Point> points){
            ValidateArg.IsNotNull(points, "points");
            SmoothedPolyline ret=null;
            Site site=null;
            foreach(Point p in points){
                if(site==null){
                    site=new Site(p);
                    ret=new SmoothedPolyline(site);
                } else {
                    Site s = new Site(p)
                    {
                        Previous = site
                    };
                    site.Next=s;
                    site=s;
                }
            }
            return ret;
        }


        readonly Site headSite = head;
        /// <summary>
        /// the first site of the polyline
        /// </summary>
        public Site HeadSite => headSite;
        /// <summary>
        /// clones the polyline
        /// </summary>
        /// <returns></returns>
        public SmoothedPolyline Clone() {
            Site h; //the site of teh clone
            Site s = headSite; //the old site
            Site prev = null;
            Site headOfTheClone=null;
            while (s != null) {
                h=s.Clone();
                h.Previous=prev;
                if (prev != null)
                {
                    prev.Next = h;
                }
                else
                {
                    headOfTheClone = h;
                }

                s = s.Next;
                prev = h;
            }
            return new SmoothedPolyline(headOfTheClone);
        }

        /// <summary>
        /// the last site of the polyline
        /// </summary>
        public Site LastSite {
            get {
                Site ret = headSite;
                while (ret.Next != null)
                {
                    ret = ret.Next;
                }

                return ret;
            }
        }

#if TEST_MSAGL
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LineSegment> GetSegments() {
            Site s0 = headSite;
            Site s1 = headSite.Next;
            while (s1 != null) {
               yield return new LineSegment(s0.Point,s1.Point);
                s0=s1;
                s1=s1.Next;
            }
        }
#endif

        /// <summary>
        /// Creates a curve by using the underlying polyline
        /// </summary>
        /// <returns></returns>
        public Curve CreateCurve() {
            Curve curve = new Curve();
            Site a = HeadSite;//the corner start
            Site b; //the corner origin
                    //the corner other end

            while (Curve.FindCorner(a, out b, out Site? c))
            {
                CubicBezierSegment bezierSeg = CreateBezierSegOnSite(b);
                if (curve.Segments.Count == 0)
                {
                    if (!ApproximateComparer.Close(a.Point, bezierSeg.Start))
                    {
                        Curve.AddLineSegment(curve, a.Point, bezierSeg.Start);
                    }
                }
                else if (!ApproximateComparer.Close(curve.End, bezierSeg.Start))
                {
                    Curve.ContinueWithLineSegment(curve, bezierSeg.Start);
                }

                curve.AddSegment(bezierSeg);
                a = b;
            }

            System.Diagnostics.Debug.Assert(a.Next.Next == null);

            if (curve.Segments.Count == 0) {
                if (!ApproximateComparer.Close(a.Point, a.Next.Point)) {
                    Curve.AddLineSegment(curve, a.Point, a.Next.Point);
                } else {
                    double w = 5;
                    curve.Segments.Add(new CubicBezierSegment(a.Point, a.Point + new Point(w, w), a.Point + new Point(-w, w), b.Point));
                }
            } else if (!ApproximateComparer.Close(curve.End, a.Next.Point))
            {
                Curve.ContinueWithLineSegment(curve, a.Next.Point);
            }

            return curve;
        }

        internal static CubicBezierSegment CreateBezierSegOnSite(Site b) {
            double kPrev = b.PreviousBezierSegmentFitCoefficient;
            double kNext = b.NextBezierSegmentFitCoefficient;
            Site? a = b.Previous;
            Site? c = b.Next;
            Point s = kPrev*a.Point + (1 - kPrev)*b.Point;
            Point e = kNext*c.Point + (1 - kNext)*b.Point;
            Point u = s*b.PreviousTangentCoefficient + (1 - b.PreviousTangentCoefficient)*b.Point;
            Point v = e*b.NextTangentCoefficient + (1 - b.NextTangentCoefficient)*b.Point;
            return new CubicBezierSegment(s, u, v, e);
        }

        #region IEnumerable<Point> Members
        /// <summary>
        /// the enumerator of the polyline corners
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Point> GetEnumerator() => new PointNodesList(headSite);

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new PointNodesList(headSite);

        #endregion
        /// <summary>
        /// returns the reversed smoothed polyline and does not change "this"
        /// </summary>
        /// <returns></returns>
        internal SmoothedPolyline Reverse()
        {
            SmoothedPolyline ret = FromPoints(this);
            Site? a = ret.HeadSite;
            Site? b = ret.LastSite;
            while(b!=null)
            {
                a.NextBezierSegmentFitCoefficient = b.PreviousBezierSegmentFitCoefficient;
                a.PreviousBezierSegmentFitCoefficient = b.NextBezierSegmentFitCoefficient;
                b = b.Previous;
                a = a.Next;
            }
            return ret;
        }
    }
}
