using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing;

namespace Microsoft.AGL.Miscellaneous.LayoutEditing
{
    /// <summary>
    /// keeps a label restore data
    /// </summary>
    public class LabelRestoreData:RestoreData  {
        private Point center;

        /// <summary>
        /// the label center
        /// </summary>
        public Point Center { get => center; set => center = value;
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="centerP"></param>
        public LabelRestoreData(Point centerP) => center = centerP;
    }
}
