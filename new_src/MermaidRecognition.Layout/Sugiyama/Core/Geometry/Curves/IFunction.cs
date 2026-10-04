
namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves {
    /// <summary>
    /// 
    /// </summary>
    public interface IFunction {
        /// <summary>
        /// Returns the falue on the curve corresponding to parameter t
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double this[double t] { get; }

        /// <summary>
        /// first derivative at t
        /// </summary>
        /// <param name="t">the parameter where the derivative is calculated</param>
        /// <returns></returns>
        double Derivative(double t);

        /// <summary>
        /// second derivative
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        double SecondDerivative(double t);

        /// <summary>
        /// third derivative
        /// </summary>
        /// <param name="t">the parameter of the derivative</param>
        /// <returns></returns>
        double ThirdDerivative(double t);

        /// <summary>
        /// the domain of the function is the [ParStart, ParEnd] segment
        /// </summary>
        double ParStart { get; }
        /// <summary>
        /// the domain of the function is the [ParStart, ParEnd] segment
        /// </summary>
        double ParEnd { get; }
    }
}
