using Microsoft.AGL.Core.Geometry;

#if DEBUG && !SILVERLIGHT  && !SHARPKIT
namespace Microsoft.AGL.DebugHelpers {
    ///<summary>
    ///</summary>
    public class DebugRectangle : DebugShape {
        /// <summary>
        /// 
        /// </summary>
        public Rectangle Rectangle { get; set; }
    }
}
#endif