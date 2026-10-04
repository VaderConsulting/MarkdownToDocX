using System.Collections.Generic;
using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// restricts the access to a port
    ///  </summary>
    public interface IPortEntry {
        /// <summary>
        /// returns the points nearby the middle of the entries
        /// </summary>
        /// <returns></returns>      
        IEnumerable<Point> GetEntryPoints();
    }
}
