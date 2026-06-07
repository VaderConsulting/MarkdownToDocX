using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

#if DEBUG && !SILVERLIGHT  && !SHARPKIT
namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
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