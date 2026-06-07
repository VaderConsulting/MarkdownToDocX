#if TEST_MSAGL
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Layout.Layered;

namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="curves"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "db")]
    public delegate void ShowDatabase(Database db, params ICurve[] curves);
}
#endif