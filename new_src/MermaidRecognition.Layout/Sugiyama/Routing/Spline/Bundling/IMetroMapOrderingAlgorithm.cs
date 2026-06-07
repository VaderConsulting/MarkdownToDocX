using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
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
