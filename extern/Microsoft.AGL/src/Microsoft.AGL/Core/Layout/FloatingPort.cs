using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// floating port: specifies that the edge is routed to the Location 
    /// </summary>
    /// <remarks>
    /// constructor
    /// </remarks>
    /// <param name="location"></param>
    /// <param name="curve">the port curve, can be null</param>
    public class FloatingPort(ICurve curve, Point location) : Port {
        ICurve curve = curve; //a curve associated with the port



        Point location = location;
        /// <summary>
        /// the location of the port
        /// </summary>
        public override Point Location => location;
        /// <summary>
        /// translate the port location by delta
        /// </summary>
        /// <param name="delta"></param>
        public virtual void Translate(Point delta) => location += delta;
        /// <summary>
        /// the port's curve
        /// </summary>
        public override ICurve Curve { get => curve; set => curve = value;
        }

        /// <summary>
        /// Return a string representation of the Port location
        /// </summary>
        /// <returns>a string representation of the Port location</returns>
        public override string ToString() => Location.ToString();
    }
}
