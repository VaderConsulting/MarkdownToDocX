using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear{
    /// <summary>
    /// keep this class internal, it not a full fledged Port
    /// </summary>
    internal class WaypointPort : FloatingPort{
        /// <summary>
        /// 
        /// </summary>
        /// <param name="location"></param>
        public WaypointPort(Point location) : base(null, location) => this.location = location;

        readonly Point location;
        /// <summary>
        /// 
        /// </summary>
        public override Point Location => location;

        public override ICurve Curve => null;
    }
}