using Microsoft.AGL.Core.Geometry.Curves;

#if DEBUG && ! SILVERLIGHT
namespace Microsoft.AGL.DebugHelpers {
    ///<summary>
    ///</summary>
    public class DebugEllipse : DebugShape {
        /// <summary>
        /// 
        /// </summary>
        public Ellipse Ellipse { get; set; }
    }
}
#endif
