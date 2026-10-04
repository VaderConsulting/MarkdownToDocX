using System;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;
using System.Diagnostics;
using MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
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

        internal Point TargetPoint => Target.Point;

        internal Point SourcePoint => Source.Point;

        internal bool IsOccupied => numberOfPassedPaths > 0;

        internal Set<CdtEdge> CrossedCdtEdges { get; set; }

        internal bool IsPassable => Target.IsTargetOfRouting || Source.IsSourceOfRouting ||
                       VisibilityEdge.IsPassable == null ||
                       VisibilityEdge.IsPassable();

        internal void AddOccupiedEdge() => numberOfPassedPaths++;

        internal void RemoveOccupiedEdge() {
            numberOfPassedPaths--;
            Debug.Assert(numberOfPassedPaths >= 0);
        }
    }
}