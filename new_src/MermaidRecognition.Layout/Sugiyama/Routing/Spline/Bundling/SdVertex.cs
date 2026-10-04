using System;
using System.Collections.Generic;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    internal class SdVertex {
        internal VisibilityVertex VisibilityVertex;
        internal List<SdBoneEdge> InBoneEdges = [];
        internal List<SdBoneEdge> OutBoneEdges = [];

        internal SdVertex Prev {
            get {
                if (PrevEdge == null) return null;
                return PrevEdge.Source == this ? PrevEdge.Target : PrevEdge.Source;
            }
        }

        internal SdBoneEdge PrevEdge { get; set; }

        internal SdVertex(VisibilityVertex visibilityVertex) => VisibilityVertex = visibilityVertex;

        internal CdtTriangle Triangle;

        internal bool IsSourceOfRouting { get; set; }

        internal bool IsTargetOfRouting { get; set; }

        internal Point Point => VisibilityVertex.Point;

        double cost;

        internal double Cost
        {
            get
            {
                if (IsSourceOfRouting) return cost;
                return Prev == null ? double.PositiveInfinity : cost;
            }

            set => cost = value;
        }

        public void SetPreviousToNull() => PrevEdge = null;
    }
}