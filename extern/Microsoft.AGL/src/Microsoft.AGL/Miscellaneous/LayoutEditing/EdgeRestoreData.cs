using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Miscellaneous.LayoutEditing
{
    /// <summary>
    /// holds the data needed to restore the edge after the editing
    /// </summary>
    public class EdgeRestoreData : RestoreData {
        Point labelCenter;

        internal EdgeRestoreData(Edge edge) {
            if (edge.UnderlyingPolyline == null) {
                Curve asCurve = (edge.Curve as Curve) ?? new Curve([edge.Curve]);
                edge.UnderlyingPolyline =
                    SmoothedPolyline.FromPoints(
                        new[] {edge.Source.Center}.Concat(Polyline.PolylineFromCurve(asCurve)).
                            Concat(new[] {edge.Target.Center}));
            }
            UnderlyingPolyline = edge.UnderlyingPolyline.Clone();

            Curve = edge.Curve.Clone();
            if (edge.EdgeGeometry.SourceArrowhead != null)
                ArrowheadAtSourcePosition = edge.EdgeGeometry.SourceArrowhead.TipPosition;
            if (edge.EdgeGeometry.TargetArrowhead != null)
                ArrowheadAtTargetPosition = edge.EdgeGeometry.TargetArrowhead.TipPosition;
            if (edge.Label != null && edge.UnderlyingPolyline != null) {
                labelCenter = edge.Label.Center;
                Curve untrimmedCurve = edge.UnderlyingPolyline.CreateCurve();
                LabelAttachmentParameter = untrimmedCurve.ClosestParameter(labelCenter);
                LabelOffsetFromTheAttachmentPoint = labelCenter - untrimmedCurve[LabelAttachmentParameter];
            }
        }

        /// <summary>
        /// the underlying polyline
        /// </summary>
        public SmoothedPolyline UnderlyingPolyline { get; set; }

        /// <summary>
        /// the initial center
        /// </summary>
        public Point LabelCenter { get => labelCenter; set => labelCenter = value;
        }

        /// <summary>
        /// the edge original curve
        /// </summary>
        public ICurve Curve { get; set; }

        /// <summary>
        /// the arrow head position at source
        /// </summary>
        public Point ArrowheadAtSourcePosition { get; set; }

        /// <summary>
        /// the arrow head position at target
        /// </summary>
        public Point ArrowheadAtTargetPosition { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Point LabelOffsetFromTheAttachmentPoint { get; set; }


        /// <summary>
        /// the closest point to the label center
        /// </summary>
        public double LabelAttachmentParameter { get; set; }
    }
}