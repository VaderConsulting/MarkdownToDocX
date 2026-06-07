#if TEST_MSAGL
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
    ///<summary>
    /// shows shapes 
    ///</summary>
    ///<param name="shapes"></param>
    public delegate void ShowDebugCurvesEnumeration(IEnumerable<DebugCurve> shapes);
}   
#endif