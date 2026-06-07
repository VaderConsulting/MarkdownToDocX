#if TEST_MSAGL
using System.Collections.Generic;

namespace Microsoft.AGL.DebugHelpers {
    ///<summary>
    /// shows shapes 
    ///</summary>
    ///<param name="shapes"></param>
    public delegate void ShowDebugCurvesEnumeration(IEnumerable<DebugCurve> shapes);
}   
#endif