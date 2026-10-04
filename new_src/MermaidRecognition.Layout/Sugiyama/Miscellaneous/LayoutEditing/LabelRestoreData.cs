using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing;

namespace MermaidRecognition.Layout.Sugiyama.Miscellaneous.LayoutEditing
{
    /// <summary>
    /// keeps a label restore data
    /// </summary>
    /// <remarks>
    /// constructor
    /// </remarks>
    /// <param name="centerP"></param>
    public class LabelRestoreData(Point centerP) : RestoreData  {
        private Point center = centerP;

        /// <summary>
        /// the label center
        /// </summary>
        public Point Center { get => center; set => center = value;
        }
    }
}
