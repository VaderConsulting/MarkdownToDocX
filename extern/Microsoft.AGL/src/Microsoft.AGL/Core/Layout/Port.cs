using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// Specifies the way an edge is connected to a curve
    /// </summary>
#if TEST_MSAGL
    [System.Serializable]
#endif
    public abstract class Port {
        
        /// <summary>
        /// Gets the point associated with the port.
        /// </summary>
        public abstract Point Location { get; }

        /// <summary>
        /// Gets the boundary curve of the port.
        /// </summary>
        public abstract ICurve Curve { get; set; }

        /// <summary>
        /// Defines one or more restrictions on where an edge can be routed relative to the port.
        /// </summary>
        public IPortEntry PortEntry { get; set; }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Location.ToString();
    }
}