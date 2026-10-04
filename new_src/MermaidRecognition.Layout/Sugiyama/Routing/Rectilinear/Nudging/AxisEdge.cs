using System;
using System.Collections.Generic;
using System.Diagnostics;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    /// <summary>
    /// a wrapper arownd VisibilityEdge representing the same edge 
    /// but oriented along the X or the Y axis
    /// </summary>
    internal class AxisEdge:VisibilityEdge {
        internal Directions Direction { get; set; }
        internal AxisEdge(VisibilityVertex source, VisibilityVertex target)
            : base(source, target){
            RightBound = double.PositiveInfinity;
            LeftBound = double.NegativeInfinity;
            Direction = CompassVector.DirectionsFromPointToPoint(source.Point, target.Point);
            Debug.Assert(Direction == Directions.East || Direction == Directions.North);
        }

        internal readonly Set<AxisEdge> RightNeighbors = [];

        internal void AddRightNeighbor(AxisEdge edge) => RightNeighbors.Insert(edge);

        internal double LeftBound { get; set; }

        internal double RightBound { get; private set; }

        readonly Set<LongestNudgedSegment> setOfLongestSegs = [];

        internal IEnumerable<LongestNudgedSegment> LongestNudgedSegments => setOfLongestSegs;

        internal void AddLongestNudgedSegment(LongestNudgedSegment segment) => setOfLongestSegs.Insert(segment);

        internal void BoundFromRight(double rightbound) {
            rightbound = Math.Max(rightbound, LeftBound);
            RightBound = Math.Min(rightbound, RightBound);
            //Debug.Assert(SegsAreFine());
        }

/*
        bool SegsAreFine() {
            foreach (var lseg in setOfLongestSegs) {
                if (lseg.GetLeftBound() > lseg.GetRightBound())
                    return false;
            }
            return true;
        }
*/

        internal void BoundFromLeft(double leftbound) {
            leftbound = Math.Min(leftbound, RightBound);
            LeftBound = Math.Max(leftbound, LeftBound);
        }
    }
}