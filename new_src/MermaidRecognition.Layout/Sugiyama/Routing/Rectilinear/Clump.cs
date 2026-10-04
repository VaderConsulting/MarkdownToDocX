//
// Clump.cs
// MSAGL class for accreting clumps of overlapped obstacles for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    /// <summary>
    /// This is the list of obstacles in the clump.
    /// </summary>
    internal class Clump : List<Obstacle> {
        internal Clump(IEnumerable<Obstacle> obstacles) => AddRange(obstacles);
#if DEBUG && TEST_MSAGL
        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format("({0}", Count);
#endif
    }
}
