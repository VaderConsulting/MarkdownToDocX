using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.DebugHelpers;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    /// <summary>
    /// 
    /// </summary>
    interface IMetroMapOrderingAlgorithm {
        IEnumerable<Metroline> GetOrder(Station u, Station v);

        /// <summary>
        /// Get the index of line on the edge (u->v) and node u
        /// </summary>
        int GetLineIndexInOrder(Station u, Station v, Metroline metroLine);
    }



}
