// #region Using directives

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.DebugHelpers;
//#endregion

namespace Microsoft.AGL.Core.Geometry.Curves {
    /// <summary>
    /// Cubic Bezier Segment
    /// </summary>
#if TEST_MSAGL
    
#endif
    public class CubicBezierSegment : ICurve {

        /// <summary>
        /// left derivative at t
        /// </summary>
        /// <param name="t">the parameter where the derivative is calculated</param>
        /// <returns></returns>
        public Point LeftDerivative(double t) => Derivative(t);

        /// <summary>
        /// right derivative at t
        /// </summary>
        /// <param name="t">the parameter where the derivative is calculated</param>
        /// <returns></returns>
        public Point RightDerivative(double t) => Derivative(t);


        /// <summary>
        /// control points
        /// </summary>
        Point[] b = new Point[4];


        /// <summary>
        /// coefficients
        /// </summary>
        Point l, e, c;

        /// <summary>
        /// get a control point
        /// </summary>
        /// <param name="controlPointIndex"></param>
        /// <returns></returns>
        public Point B(int controlPointIndex) => b[controlPointIndex];
        /// <summary>
        /// ToString
        /// </summary>
        /// <returns></returns>
        override public string ToString() => string.Format(CultureInfo.InvariantCulture, "(Bezie{0},{1},{2},{3})", b[0], b[1], b[2], b[3]);
        ParallelogramNodeOverICurve pBoxNode;
        /// <summary>
        /// A tree of ParallelogramNodes covering the curve. 
        /// This tree is used in curve intersections routines.
        /// </summary>
        /// <value></value>
        public ParallelogramNodeOverICurve ParallelogramNodeOverICurve {
            get {
#if PPC 
                lock(this){
#endif
                if (pBoxNode != null)
                    return pBoxNode;
                return pBoxNode = ParallelogramNodeOverICurve.CreateParallelogramNodeForCurveSegment(this);
#if PPC
                }
#endif
            }
        }
        /// <summary>
        /// Returns the point on the curve corresponding to parameter t
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Point this[double t] {
            get {
                double t2 = t * t;
                double t3 = t2 * t;
                return l * t3 + e * t2 + c * t + b[0];
            }
        }

        static void AdjustParamTo01(ref double u) {
            if (u > 1)
                u = 1;
            else if (u < 0)
                u = 0;
        }
        //throw away the segments [0,u] and [v,1] of the segment

        /// <summary>
        /// Returns the trimmed curve
        /// </summary>
        /// <param name="u"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public ICurve Trim(double u, double v) {

            AdjustParamTo01(ref u);
            AdjustParamTo01(ref v);

            if (u > v)
                return Trim(v, u);

            if (u > 1.0 - ApproximateComparer.Tolerance)
                return new CubicBezierSegment(b[3], b[3], b[3], b[3]);

            Point[] b1 = new Point[3];
            Point[] b2 = new Point[2];
            Point pv = Casteljau(u, b1, b2);

            //this will be the trim to [v,1]
            CubicBezierSegment trimByU = new CubicBezierSegment(pv, b2[1], b1[2], b[3]);


            //1-v is not zero here because we have handled already the case v=1
            Point pu = trimByU.Casteljau((v - u) / (1.0 - u), b1, b2);

            return new CubicBezierSegment(trimByU.b[0], b1[0], b2[0], pu);

        }

        /// <summary>
        /// Not Implemented: Returns the trimmed curve, wrapping around the end if start is greater than end.
        /// </summary>
        /// <param name="start">The starting parameter</param>
        /// <param name="end">The ending parameter</param>
        /// <returns>The trimmed curve</returns>
        public ICurve TrimWithWrap(double start, double end) => throw new NotImplementedException();

        //array for Casteljau method
        internal Point Casteljau(double t, Point[] b1, Point[] b2) {

            double f = 1.0 - t;
            for (int i = 0; i < 3; i++)
                b1[i] = b[i] * f + b[i + 1] * t;

            for (int i = 0; i < 2; i++)
                b2[i] = b1[i] * f + b1[i + 1] * t;

            return b2[0] * f + b2[1] * t;

        }
        /// <summary>
        /// first derivative
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Point Derivative(double t) => 3 * l * t * t + 2 * e * t + c;
        /// <summary>
        /// second derivative
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Point SecondDerivative(double t) => 6 * l * t + 2 * e;

        /// <summary>
        /// third derivative
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Point ThirdDerivative(double t) => 6 * l;

        /// <summary>
        /// the constructor
        /// </summary>
        /// <param name="b0"></param>
        /// <param name="b1"></param>
        /// <param name="b2"></param>
        /// <param name="b3"></param>
        public CubicBezierSegment(Point b0, Point b1, Point b2, Point b3) {

            b[0] = b0;
            b[1] = b1;
            b[2] = b2;
            b[3] = b3;
            c = 3 * (b[1] - b[0]);
            e = 3 * (b[2] - b[1]) - c;
            l = b[3] - b[0] - c - e;
        }

        /// <summary>
        /// this[ParStart]
        /// </summary>
        public Point Start => b[0];

        /// <summary>
        /// this[ParEnd]
        /// </summary>
        public Point End => b[3];


        double parStart;

        /// <summary>
        /// the start of the parameter domain
        /// </summary>
        public double ParStart { get => parStart; set => parStart = value;
        }

        double parEnd = 1;

        /// <summary>
        /// the end of the parameter domain
        /// </summary>
        public double ParEnd { get => parEnd; set => parEnd = value;
        }

        /// <summary>
        /// this[Reverse[t]]=this[ParEnd+ParStart-t]
        /// </summary>
        /// <returns></returns>
        public ICurve Reverse() => new CubicBezierSegment(b[3], b[2], b[1], b[0]);

        /// <summary>
        /// Returns the curved moved by delta
        /// </summary>
        public void Translate(Point delta) {
            b[0] += delta;
            b[1] += delta;
            b[2] += delta;
            b[3] += delta;
            c = 3 * (b[1] - b[0]);
            e = 3 * (b[2] - b[1]) - c;
            l = b[3] - b[0] - c - e;

            pBoxNode = null;
        }

        /// <summary>
        /// Returns the curved scaled by x and y
        /// </summary>
        /// <param name="xScale"></param>
        /// <param name="yScale"></param>
        /// <returns></returns>
        public ICurve ScaleFromOrigin(double xScale, double yScale) => new CubicBezierSegment(Point.Scale(xScale, yScale, b[0]), Point.Scale(xScale, yScale, b[1]), Point.Scale(xScale, yScale, b[2]), Point.Scale(xScale, yScale, b[3]));

        /// <summary>
        /// Offsets the curve in the direction of dir
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public ICurve OffsetCurve(double offset, Point dir) => null;

        /// <summary>
        /// return length of the curve segment [start,end] 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public double LengthPartial(double start, double end) => Trim(start, end).Length;
        /// <summary>
        /// Get the length of the curve
        /// </summary>
        public double Length => LengthOnControlPolygon(b[0], b[1], b[2], b[3]);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="b0"></param>
        /// <param name="b1"></param>
        /// <param name="b2"></param>
        /// <param name="b3"></param>
        /// <returns></returns>
        static double LengthOnControlPolygon(Point b0, Point b1, Point b2, Point b3) {
            double innerCordLength = (b3- b0).Length;
            double controlPointPolygonLength = (b1 - b0).Length + (b2 - b1).Length + (b3 - b2).Length;
            if (controlPointPolygonLength - innerCordLength > Curve.LineSegmentThreshold) {
                Point mb0 = (b0 + b1)/2;
                Point mb1 = (b1 + b2)/2;
                Point mb2 =(b2 + b3)/2;
                Point mmb0 = (mb0 + mb1)/2;
                Point mmb1 = (mb2 + mb1)/2;
                Point mmmb0 = (mmb0 + mmb1)/2;
//                LayoutAlgorithmSettings.ShowDebugCurves(new DebugCurve(100, 2, "blue", new CubicBezierSegment(b0, b1, b2, b3)), new DebugCurve(100, 1, "red", new CubicBezierSegment(b0, mb0, mmb0, mmmb0)), new DebugCurve(100, 1, "green", new CubicBezierSegment(mmmb0, mmb1, mb2, b3)));
                return LengthOnControlPolygon(b0, mb0, mmb0, mmmb0) + LengthOnControlPolygon(mmmb0, mmb1, mb2, b3);
            }

            return (controlPointPolygonLength + innerCordLength) / 2;
        }

        /// <summary>
        /// the segment bounding box
        /// </summary>
        public Rectangle BoundingBox {
            get {
                Rectangle ret = new Rectangle(b[0], b[1]);
                ret.Add(b[2]);
                ret.Add(b[3]);
                return ret;
            }
        }


        /// <summary>
        /// Return the transformed curve
        /// </summary>
        /// <param name="transformation"></param>
        /// <returns>the transformed curve</returns>
        public ICurve Transform(PlaneTransformation transformation) => new CubicBezierSegment(transformation * b[0], transformation * b[1], transformation * b[2], transformation * b[3]);

        /// <summary>
        /// returns a parameter t such that the distance between curve[t] and targetPoint is minimal 
        /// and t belongs to the closed segment [low,high]
        /// </summary>
        /// <param name="targetPoint">the point to find the closest point</param>
        /// <param name="high">the upper bound of the parameter</param>
        /// <param name="low">the low bound of the parameter</param>
        /// <returns></returns>
        public double ClosestParameterWithinBounds(Point targetPoint, double low, double high) {
            System.Diagnostics.Debug.Assert(high <= 1 && low >= 0);
            System.Diagnostics.Debug.Assert(low <= high);
            double t = (high-low) / 8;
            double closest = 0;
            double minDist = double.MaxValue;
            for (int i = 0; i < 9; i++) {
                Point p = targetPoint - this[i * t + low];
                double d = p * p;
                if (d < minDist) {
                    minDist = d;
                    closest = i * t + low;
                }
            }
            return ClosestPointOnCurve.ClosestPoint(this, targetPoint, closest, low, high);
        }


        /// <summary>
        /// clones the curve. 
        /// </summary>
        /// <returns>the cloned curve</returns>
        public ICurve Clone() => new CubicBezierSegment(b[0], b[1], b[2], b[3]);

        /// <summary>
        /// the signed curvature of the segment at t
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double Curvature(double t) {
            System.Diagnostics.Debug.Assert(t >= ParStart && t <= ParEnd);

            double den = G(t);

            System.Diagnostics.Debug.Assert(Math.Abs(den) > 0.00001);

            return F(t) / den;
        }


        double F(double t) => Xp(t) * Ypp(t) - Yp(t) * Xpp(t);

        /// <summary>
        /// G(t) is the denomenator of the curvature
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double G(double t) {
            double xp = Xp(t);
            double yp = Yp(t);
            double den = (xp * xp + yp * yp);
            return Math.Sqrt(den * den * den);
        }

        /// <summary>
        /// the first derivative of x-coord
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double Xp(double t) => 3 * l.X * t * t + 2 * e.X * t + c.X;

        /// <summary>
        /// the second derivativ of y-coordinate
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double Ypp(double t) => 6 * l.Y * t + 2 * e.Y;

        /// <summary>
        /// the first derivative of y-coord
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double Yp(double t) => 3 * l.Y * t * t + 2 * e.Y * t + c.Y;

        /// <summary>
        /// the seconde derivative of x coord
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double Xpp(double t) => 6 * l.X * t + 2 * e.X;

        /// <summary>
        /// the third derivative of x coordinate
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double Xppp(double t) => 6 * l.X;

        /// <summary>
        /// the third derivative of y coordinate
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double Yppp(double t) => 6 * l.Y;


        /// <summary>
        /// the derivative of the curvature at t
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double CurvatureDerivative(double t) {
            // we need to calculate the derivative of f/g where f=xp* ypp-yp*xpp and g=(xp*xp+yp*yp)^(3/2)
            double h = G(t);
            return (Fp(t) * h - Gp(t) * F(t)) / (h * h);
        }




        double Fp(double t) => Xp(t) * Yppp(t) - Yp(t) * Xppp(t);

        double Fpp(double t) => Xpp(t) * Yppp(t) // + Xp(t) * Ypppp(t)=0
                - Ypp(t) * Xppp(t);//- Yp(t) * Xpppp(t)=0


        /// <summary>
        /// returns a parameter t such that the distance between curve[t] and a is minimal
        /// </summary>
        /// <param name="targetPoint"></param>
        /// <returns></returns>
        public double ClosestParameter(Point targetPoint) {
            double t = 1.0 / 8;
            double closest = 0;
            double minDist = double.MaxValue;
            for (int i = 0; i < 9; i++) {
                Point p = targetPoint - this[i * t];
                double d = p * p;
                if (d < minDist) {
                    minDist = d;
                    closest = i * t;
                }
            }
            return ClosestPointOnCurve.ClosestPoint(this, targetPoint, closest, 0, 1);
        }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<Tuple<double, double>> MaximalCurvaturePoints {
            get {

                List<Tuple<double, double>> maxCurveCandidates = [];

                
                int n = 8;
                double d = 1.0 / n;
                double prev = -1;//out of the domain
                for (int i = 0; i < n; i++) {
                    double start = i * d;
                    double end = start + d;
                    if (RootFinder.TryToFindRoot(new CurvatureDerivative(this, start, end), start, end, (start + end) / 2, out double x))
                    {
                        if (x != prev)
                        {
                            prev = x;
                            maxCurveCandidates.Add(new Tuple<double, double>(x, Curvature(x)));
                        }
                    }
                }
                maxCurveCandidates.Add(new Tuple<double, double>(ParStart, Curvature(ParStart)));
                maxCurveCandidates.Add(new Tuple<double, double>(ParEnd, Curvature(ParEnd)));

                double maxCur = maxCurveCandidates.Max(l => Math.Abs(l.Item2));

                return from v in maxCurveCandidates where Math.Abs(v.Item2) == maxCur select v;
            }

        }

        #region ICurve Members

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double CurvatureSecondDerivative(double t) {
            double g = G(t);
            return (Qp(t) * g - 2 * Q(t) * Gp(t)) / (g * g * g);
        }

        double Q(double t) => Fp(t) * G(t) - Gp(t) * F(t);

        double Qp(double t) => Fpp(t) * G(t) - Gpp(t) * F(t);

        double Gpp(double t) {
            double xp = Xp(t);
            double yp = Yp(t);
            double xpp = Xpp(t);
            double ypp = Ypp(t);
            double xppp = Xppp(t);
            double yppp = Yppp(t);
            double u = Math.Sqrt(xp * xp + yp * yp);
            double v = xp * xpp + yp * ypp;
            return 3 * ((v * v) / u + u * (xpp * xpp + xp * xppp + ypp * ypp + yp * yppp));

        }

        double Gp(double t) {
            double xp = Xp(t);
            double yp = Yp(t);
            double xpp = Xpp(t);
            double ypp = Ypp(t);
            return 3 * Math.Sqrt(xp * xp + yp * yp) * (xp * xpp + yp * ypp);
        }


        #endregion


        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public double GetParameterAtLength(double length) {
            double low = 0;
            double upper = 1;
            while (upper - low > ApproximateComparer.Tolerance) {
                double middle = (upper + low)/2;
                int err = EvaluateError(length, middle);
                if (err > 0)
                    upper = middle;
                else if (err < 0)
                    low = middle;
                else
                    return middle;
            }

            return (low + upper)/2;
        }

        int EvaluateError(double length, double t) {
            //todo: this is a slow version!
            double f = 1 - t;
            Point mb0 = f*b[0] + t*b[1];
            Point mb1 = f*b[1] + t*b[2];
            Point mb2 = f*b[2] + t*b[3];
            Point mmb0 = f * mb0 + t * mb1;
            Point mmb1 = f * mb1 + t * mb2;
            Point mmmb0 = f * mmb0 + t * mmb1;

            double lengthAtT = LengthOnControlPolygon(b[0], mb0, mmb0, mmmb0);
            
            if (lengthAtT > length + ApproximateComparer.DistanceEpsilon)
                return 1;

            if (lengthAtT < length - ApproximateComparer.DistanceEpsilon)
                return -1;

            return 0;
        }
    }
}