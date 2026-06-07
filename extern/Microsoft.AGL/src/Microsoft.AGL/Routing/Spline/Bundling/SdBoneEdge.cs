using System;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Routing.Visibility;
using System.Diagnostics;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    [DebuggerDisplay("({SourcePoint.X},{SourcePoint.Y})->({TargetPoint.X},{TargetPoint.Y})")]
    internal class SdBoneEdge {
        internal readonly VisibilityEdge VisibilityEdge;
        internal readonly SdVertex Source;
        internal readonly SdVertex Target;
        int numberOfPassedPaths;

        internal SdBoneEdge(VisibilityEdge visibilityEdge, SdVertex source, SdVertex target) {
            VisibilityEdge = visibilityEdge;
            Source = source;
            Target = target;
        }

        internal Point TargetPoint {
            get { return Target.Point; }
        }

        internal Point SourcePoint {
            get { return Source.Point; }
        }

        internal bool IsOccupied {
            get { return numberOfPassedPaths > 0; }
        }

        internal Set<CdtEdge> CrossedCdtEdges { get; set; }

        internal bool IsPassable {
            get {
                return Target.IsTargetOfRouting || Source.IsSourceOfRouting ||
                       VisibilityEdge.IsPassable == null ||
                       VisibilityEdge.IsPassable();
            }
        }

        internal void AddOccupiedEdge() {
            numberOfPassedPaths++;
        }

        internal void RemoveOccupiedEdge() {
            numberOfPassedPaths--;
            Debug.Assert(numberOfPassedPaths >= 0);
        }
    }
}