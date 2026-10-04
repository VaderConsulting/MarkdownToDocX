using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Routing;

namespace MermaidRecognition.Layout.Sugiyama.Miscellaneous.LayoutEditing
{
    /// <summary>
    /// node restore data
    /// </summary>
    public class NodeRestoreData:RestoreData {

        internal NodeRestoreData(ICurve boundaryCurve) => this.boundaryCurve = boundaryCurve;

        private ICurve boundaryCurve;

        /// <summary>
        /// node boundary curve
        /// </summary>
        public ICurve BoundaryCurve { get => boundaryCurve; set => boundaryCurve = value;
        }
    }
}
