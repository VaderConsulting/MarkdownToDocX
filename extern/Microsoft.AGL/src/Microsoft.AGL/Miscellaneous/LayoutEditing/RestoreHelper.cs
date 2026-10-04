using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Miscellaneous.LayoutEditing
{
    /// <summary>
    /// Helper class for creating RestoreData objects from GeometryGraph objects.
    /// </summary>
    public static class RestoreHelper
    {
        /// <summary>
        /// creates node restore datat
        /// </summary>
        /// <returns></returns>
        public static RestoreData GetRestoreData(Node node) => new NodeRestoreData(node.BoundaryCurve.Clone());

        /// <summary>
        /// gets restore data for an edge
        /// </summary>
        /// <returns></returns>
        public static RestoreData GetRestoreData(Edge edge) => new EdgeRestoreData(edge);

        /// <summary>
        /// creates graph restore data
        /// </summary>
        /// <returns></returns>
        public static RestoreData GetRestoreData(GeometryGraph graph) => new GraphRestoreData();

        /// <summary>
        /// creates label restore data
        /// </summary>
        /// <returns></returns>
        public static RestoreData GetRestoreData(Label label) => new LabelRestoreData(label.Center);

        /// <summary>
        /// calculates the restore data
        /// </summary>
        /// <returns></returns>
        public static RestoreData GetRestoreData(GeometryObject geometryObject)
        {
            if (geometryObject is Node node)
            {
                return GetRestoreData(node);
            }

            if (geometryObject is Edge edge)
            {
                return GetRestoreData(edge);
            }

            if (geometryObject is Label label)
            {
                return GetRestoreData(label);
            }

            if (geometryObject is GeometryGraph graph)
            {
                return GetRestoreData(graph);
            }

            return null;
        }
    }
}
