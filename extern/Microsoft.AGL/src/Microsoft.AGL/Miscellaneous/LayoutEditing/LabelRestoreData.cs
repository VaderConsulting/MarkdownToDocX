using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing;

namespace Microsoft.AGL.Miscellaneous.LayoutEditing
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
