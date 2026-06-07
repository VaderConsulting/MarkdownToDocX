using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

#if DEBUG && ! SILVERLIGHT
namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
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
