using System;
using System.Collections.Generic;
using System.Globalization;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered {
    /// <summary>
    /// Defines the anchors for a node; anchors can be not symmetrical in general
    /// 
    ///          |TopAnchor
    ///Left anchor|
    /// ======Origin==================RightAnchor
    ///          |
    ///          |
    ///          |BottomAnchor
    /// </summary>
#if TEST_MSAGL
    public
#else
    internal
#endif
        class Anchor {
        /// <summary>
        /// ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString() => "la:ra " +
              la.ToString("#.##", CultureInfo.InvariantCulture) + " " + ra.ToString("#.##", CultureInfo.InvariantCulture) + " ta:ba " + ta.ToString("#.##", CultureInfo.InvariantCulture) + " " + ba.ToString("#.##", CultureInfo.InvariantCulture) + " x:y " + x.ToString("#.##", CultureInfo.InvariantCulture) + " " + y.ToString("#.##", CultureInfo.InvariantCulture);

        double la;
        double ra;
        double ta;
        double ba;

        double labelCornersPreserveCoefficient;

        /// <summary>
        /// distance for the center of the node to its left boundary
        /// </summary>
        public double LeftAnchor
        {
            get => la;
            set
            {
                //the absence of this check allows a situation when an edge crosses its label or 
                // a label which does not belong to the edge
                //       if(value<-Curve.DistEps)
                //       throw new Exception("assigning negative value to a anchor");
                la = Math.Max(value, 0); ;
            }
        }

        /// <summary>
        /// distance from the center of the node to its right boundary
        /// </summary>
        public double RightAnchor { get => ra; set =>
                                                   //   if(value<-Curve.DistEps)
                                                   //   throw new Exception("assigning negative value to a anchor: "+value );
                                                   ra = Math.Max(value, 0);
        }

        /// <summary>
        /// distance from the center of the node to its top boundary
        /// </summary>
        public double TopAnchor { get => ta; set =>
                                                 //if(value<-Curve.DistEps)
                                                 //throw new Exception("assigning negative value to a anchor");
                                                 ta = Math.Max(value, 0);
        }

        /// <summary>
        /// distance from the center of the node to it bottom boundary
        /// </summary>
        public double BottomAnchor { get => ba; set =>

                                                    //if(value<-Curve.DistEps)
                                                    //throw new InvalidOperationException();//"assigning negative value to a anchor");
                                                    ba = Math.Max(value, 0);
        }


        /// <summary>
        /// Left boundary of the node
        /// </summary>
        public double Left => x - la;

        /// <summary>
        /// right boundary of the node
        /// </summary>
        public double Right => x + ra;

        /// <summary>
        /// top boundary of the node
        /// </summary>
        public double Top { get => y + ta; set => y += value - Top;
        }

        /// <summary>
        /// bottom of the node
        /// </summary>
        public double Bottom { get => y - ba; set => y += value - Bottom;
        }

        /// <summary>
        /// Left top corner
        /// </summary>
        public Point LeftTop => new Point(Left, Top);
        /// <summary>
        /// Left bottom of the node
        /// </summary>
        public Point LeftBottom => new Point(Left, Bottom);
        /// <summary>
        /// Right bottom of the node
        /// </summary>
        public Point RightBottom => new Point(Right, Bottom);

        Node node;

        internal Node Node
        {
            get => node;
            set
            {
                node = value;
                polygonalBoundary = null;
            }
        }

        /// <summary>
        /// Right top of the node
        /// </summary>
        public Point RightTop => new Point(Right, Top);

        /// <summary>
        /// an empty constructor
        /// </summary>
        public Anchor(double labelCornersPreserveCoefficient) => this.labelCornersPreserveCoefficient = labelCornersPreserveCoefficient;

        /// <summary>
        /// constructor
        /// </summary>
        public Anchor(double leftAnchor, double rightAnchor,
            double topAnchor, double bottomAnchor, Node node, double labelCornersPreserveCoefficient) {
            la = leftAnchor;
            ra = rightAnchor;
            ta = topAnchor;
            ba = bottomAnchor;
            Node = node;
            this.labelCornersPreserveCoefficient = labelCornersPreserveCoefficient;
        }

        double x;
        /// <summary>
        /// the x position
        /// </summary>
        internal double X
        {
            get => x;

            set
            {
                polygonalBoundary = null;
                x = value;
            }
        }

        double y;
        /// <summary>
        /// the y position
        /// </summary>
        internal double Y
        {
            get => y;

            set
            {
                polygonalBoundary = null;
                y = value;
            }
        }

        /// <summary>
        /// Center of the node
        /// </summary>
        public Point Origin => new Point(x, y);

        bool alreadySitsOnASpline;

        /// <summary>
        /// signals if the spline has been routed already through the node
        /// </summary>
        public bool AlreadySitsOnASpline { get => alreadySitsOnASpline; set => alreadySitsOnASpline = value;
        }

        /// <summary>
        /// node widths
        /// </summary>
        public double Width => la + ra;

        /// <summary>
        /// node height
        /// </summary>
        public double Height => ta + ba;


        /// <summary>
        /// set to true if the anchor has been introduced for a label
        /// </summary>
        public bool RepresentsLabel => LabelToTheRightOfAnchorCenter || LabelToTheLeftOfAnchorCenter;

        bool labelIsToTheLeftOfTheSpline;

        /// <summary>
        /// An anchor for an edge label with the label to the right of the spline has its height equal to the one of the label
        /// Its leftAnchor is a reserved space for the spline and the rightAnchor is equal to the label width.
        /// </summary>
        internal bool LabelToTheLeftOfAnchorCenter { get => labelIsToTheLeftOfTheSpline; set => labelIsToTheLeftOfTheSpline = value;
        }

        bool labelIsToTheRightOfTheSpline;

        /// <summary>
        /// An anchor for an edge label with the label to the left of the spline has its height equal to the one of the label
        /// Its rightAnchor is a reserved space for the spline and the leftAnchor is equal to the label width.
        /// </summary>
        internal bool LabelToTheRightOfAnchorCenter { get => labelIsToTheRightOfTheSpline; set => labelIsToTheRightOfTheSpline = value;
        }

        internal bool HasLabel => LabelToTheRightOfAnchorCenter || LabelToTheLeftOfAnchorCenter;

        internal double LabelWidth {
            get {
                if (LabelToTheLeftOfAnchorCenter)
                {
                    return LeftAnchor;
                }

                if (LabelToTheRightOfAnchorCenter)
                {
                    return RightAnchor;
                }

                throw new InvalidOperationException();
            }
        }

        Polyline polygonalBoundary;
        /// <summary>
        /// the polygon representing the boundary of a node
        /// </summary>
#if TEST_MSAGL
        public
#else
        internal
#endif
 Polyline PolygonalBoundary {
            get {
                if (polygonalBoundary != null)
                {
                    return polygonalBoundary;
                }

                return polygonalBoundary = Pad(CreatPolygonalBoundaryWithoutPadding(),Padding);
            }
        }

        static Polyline Pad(Polyline curve, double padding) {
            if (padding == 0)
            {
                return curve;
            }

            if (CurveIsConvex(curve)) {
                return PadConvexCurve(curve, padding);
            } else
            {
                return PadConvexCurve(Curve.StandardRectBoundary(curve), padding);
            }
        }

        static void PadCorner(Polyline poly, PolylinePoint p0, PolylinePoint p1, PolylinePoint p2, double padding) {
            int numberOfPoints = GetPaddedCorner(p0, p1, p2, out Point a, out Point b, padding);
            poly.AddPoint(a);
            if (numberOfPoints==2)
            {
                poly.AddPoint(b);
            }
        }


        static  Polyline PadConvexCurve(Polyline poly, double padding) { 
            Polyline ret = new Polyline();

            PadCorner(ret, poly.EndPoint.Prev, poly.EndPoint, poly.StartPoint, padding);
            PadCorner(ret, poly.EndPoint, poly.StartPoint, poly.StartPoint.Next, padding);

            for (PolylinePoint pp = poly.StartPoint; pp.Next.Next != null; pp = pp.Next)
            {
                PadCorner(ret, pp, pp.Next, pp.Next.Next, padding);
            }

            ret.Closed = true;
            return ret;

        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="third"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="padding"></param>
        /// <returns>number of new points</returns>
        static int GetPaddedCorner(PolylinePoint first, PolylinePoint second, PolylinePoint third, out Point a, out Point b,
            double padding) {
            Point u = first.Point;
            Point v = second.Point;
            Point w = third.Point;
            System.Diagnostics.Debug.Assert(Point.GetTriangleOrientation(u, v, w) == TriangleOrientation.Clockwise);
            
            Point uvPerp = (v - u).Rotate(Math.PI / 2).Normalize();
            ////uvPerp has to look outside of the curve
            //if (uvPerp * (v - Origin) < 0)
            //    uvPerp *= -1;

            Point l = (v - u).Normalize() + (v - w).Normalize();
            if (l.Length < ApproximateComparer.IntersectionEpsilon) {
                a = b = v + padding * uvPerp;
                return 1;
            }
            Point d = l.Normalize() * padding;
            Point dp = d.Rotate(Math.PI / 2);

            //look for a in the form d+x*dp
            //we have:  Padding=(d+x*dp)*uvPerp
            double xp = (padding - d * uvPerp) / (dp * uvPerp);
            a = d + xp * dp + v;
            b = d - xp * dp + v;
            return 2; //number of points to add 
        }

        static bool CurveIsConvex(Polyline poly) {
            TriangleOrientation orientation = Point.GetTriangleOrientation(poly.EndPoint.Point, poly.StartPoint.Point, poly.StartPoint.Next.Point);
            if (Point.GetTriangleOrientation(poly.EndPoint.Prev.Point, poly.EndPoint.Point, poly.StartPoint.Point) != orientation)
            {
                return false;
            }

            PolylinePoint pp = poly.StartPoint;
            while (pp.Next.Next != null) {
                if (Point.GetTriangleOrientation(pp.Point, pp.Next.Point, pp.Next.Next.Point) != orientation)
                {
                    return false;
                }

                pp = pp.Next;
            }

            return true;
        }


        //private static double TurnAfterSeg(Curve curve, int i) {
        //    return Point.SignedDoubledTriangleArea(curve.Segments[i].Start, curve.Segments[i].End, curve.Segments[(i + 1) / curve.Segments.Count].End);
        //}

        private Polyline CreatPolygonalBoundaryWithoutPadding() {
            Polyline ret;
            if (HasLabel)
            {
                ret = LabelToTheLeftOfAnchorCenter ? PolygonOnLeftLabel() : PolygonOnRightLabel();
            }
            else if (NodeBoundary == null)
            {
                ret = StandardRectBoundary();
            }
            else
            {
                ret = Curve.PolylineAroundClosedCurve(NodeBoundary);
            }

            return ret;
        }

        private Polyline StandardRectBoundary() {
            Polyline poly = new Polyline();
            poly.AddPoint(LeftTop);
            poly.AddPoint(RightTop);
            poly.AddPoint(RightBottom);
            poly.AddPoint(LeftBottom);
            poly.Closed = true;
            return poly;
        }

        private Polyline PolygonOnLeftLabel() {
            Polyline poly = new Polyline();
            double t = Left + (1 - labelCornersPreserveCoefficient) * LabelWidth;
            poly.AddPoint(new Point(t, Top));
            poly.AddPoint(RightTop);
            poly.AddPoint(RightBottom);
            poly.AddPoint(new Point(t, Bottom));
            poly.AddPoint(new Point(Left, Y));
            poly.Closed = true;
            return poly;
        }

        private Polyline PolygonOnRightLabel() {
            Polyline poly = new Polyline();
            double t = Right - (1 - labelCornersPreserveCoefficient) * LabelWidth;
            poly.AddPoint(t, Top);
            poly.AddPoint(Right, Y);
            poly.AddPoint(t, Bottom);
            poly.AddPoint(Left, Bottom);
            poly.AddPoint(Left, Top);
            poly.Closed = true;
            return poly;

        }


        internal ICurve NodeBoundary => Node != null ? Node.BoundaryCurve : null;


        double padding;
        /// <summary>
        /// node padding
        /// </summary>
        public double Padding { get => padding; set => padding = value;
        }

        internal void Move(Point p){
            X += p.X;
            Y += p.Y;
        }

        internal void MoveTo(Point p) {
            X = p.X;
            Y = p.Y;
        }

#if TEST_MSAGL

        /// <summary>
        /// UserData
        /// </summary>
        public object UserData { get; set; }

#endif

    }
}
