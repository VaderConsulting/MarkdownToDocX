using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Routing.Rectilinear{
    /// <summary>
    /// keep this class internal, it not a full fledged Port
    /// </summary>
    internal class WaypointPort : FloatingPort{
/// <summary>
/// 
/// </summary>
/// <param name="location"></param>
        public WaypointPort(Point location):base(null,location){
           this.location = location;
        }

        readonly Point location;
        /// <summary>
        /// 
        /// </summary>
        public override Point Location {
            get { return location; }            
        }

        public override ICurve Curve {
            get { return null; }
        }
    }
}