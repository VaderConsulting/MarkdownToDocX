using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Visibility;

namespace Microsoft.AGL.Routing.Rectilinear {
    /// <summary>
    /// This vertex class is used in rectilinear shortest paths
    /// </summary>
    internal class VisibilityVertexRectilinear:VisibilityVertex {
        internal VisibilityVertexRectilinear(Point point) : base(point) {}

        internal VertexEntry[] VertexEntries { get; set; }
        
        internal void SetVertexEntry(VertexEntry entry) {
            if (this.VertexEntries == null) {
                this.VertexEntries = new VertexEntry[4];
            }
            this.VertexEntries[CompassVector.ToIndex(entry.Direction)] = entry;
        }

        internal void RemoveVertexEntries() {
            this.VertexEntries = null;
        }

    }
}   
