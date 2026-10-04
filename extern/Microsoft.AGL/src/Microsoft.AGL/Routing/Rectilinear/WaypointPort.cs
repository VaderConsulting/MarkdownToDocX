using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Routing.Rectilinear{
    /// <summary>
    /// keep this class internal, it not a full fledged Port
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="location"></param>
    internal class WaypointPort(Point location) : FloatingPort(null, location){
        readonly Point location = location;
        /// <summary>
        /// 
        /// </summary>
        public override Point Location => location;

        public override ICurve Curve => null;
    }
}