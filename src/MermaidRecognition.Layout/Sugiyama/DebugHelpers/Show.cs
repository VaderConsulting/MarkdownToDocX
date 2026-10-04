#if TEST_MSAGL
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
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