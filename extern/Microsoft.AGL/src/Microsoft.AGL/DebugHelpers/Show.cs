#if TEST_MSAGL
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.DebugHelpers {
    /// <summary>
    /// shows curves
    /// </summary>
    /// <param name="curves"></param>
    public delegate void Show(params ICurve[] curves);

    ///<summary>
    ///</summary>
    ///<param name="graph"></param>
    public delegate void ShowGraph(GeometryGraph graph);
}
#endif