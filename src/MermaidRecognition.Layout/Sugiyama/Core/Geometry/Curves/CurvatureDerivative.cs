using System;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="curve"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    public class CurvatureDerivative(ICurve curve, double start, double end) : IFunction {
        ICurve curve = curve;
        double start = start;
        double end = end;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double this[double t] {
            get { return curve.CurvatureDerivative(t); }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double Derivative(double t) => curve.CurvatureSecondDerivative(t);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double SecondDerivative(double t) => throw new NotImplementedException();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double ThirdDerivative(double t) => throw new NotImplementedException();
        /// <summary>
        /// 
        /// </summary>
        public double ParStart => start;
        /// <summary>
        /// 
        /// </summary>
        public double ParEnd => end;

    }
}
