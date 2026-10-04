
namespace Microsoft.AGL.Core.Geometry.Curves {
    /// <summary>
    /// used to calculate the around curve 
    /// </summary>
    internal class CurveTangent {
        internal Point touchPoint;

        internal Point direction;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="touch"></param>
        /// <param name="direction"></param>
        internal CurveTangent(Point touch, Point direction) {
            touchPoint = touch;
            this.direction = direction;
        }
    }
}
