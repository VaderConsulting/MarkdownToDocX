using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Layout.LargeGraphLayout;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// facilitates large graph browsing
    /// </summary>
    public class LgNodeInfo : LgInfoBase {
        //these needed for shortest path calculations
        internal Edge Prev; 
        internal double Cost;
        internal bool Processed;
        /// <summary>
        /// underlying geometry node
        /// </summary>
        public Node GeometryNode { get; set; }

        public bool Selected;

        public ICurve BoundaryCurve => BoundaryOnLayer;


        internal LgNodeInfo Parent;


        internal LgNodeInfo(Node geometryNode) => GeometryNode = geometryNode;//OriginalCurveOfGeomNode = geometryNode.BoundaryCurve.Clone();

        /// <summary>
        /// the center of the node
        /// </summary>
        public Point Center => GeometryNode.Center;


        /// <summary>
        /// 
        /// </summary>
        public Rectangle BoundingBox => BoundaryOnLayer.BoundingBox;

        /// <summary>
        /// override the string method
        /// </summary>
        /// <returns></returns>
        public override string ToString() => GeometryNode.ToString();

        public double LabelVisibleFromScale = 0.0;

        public double LabelWidthToHeightRatio = 1.0;

        public LabelPlacement LabelPosition = LabelPlacement.Top;

        public enum LabelPlacement
        {
            Top, Bottom, Left, Right
        };

        public Point LabelOffset
        {
            get
            {
                switch (LabelPosition)
                {
                    case LabelPlacement.Top:
                        return new Point(0, 0.5);
                    case LabelPlacement.Bottom:
                        return new Point(0, -0.5);
                    case LabelPlacement.Left:
                        return new Point(-0.5, 0);
                    default:
                        return new Point(0.5, 0);
                }
            }
            set
            {
                if (Math.Abs(value.X) < 0.1 && value.Y > 0.4)
                {
                    LabelPosition = LabelPlacement.Top;
                }
                else if (Math.Abs(value.X) < 0.1 && value.Y < -0.4) {
                    LabelPosition = LabelPlacement.Bottom;
                } 
                else if (Math.Abs(value.Y) < 0.1 && value.X > 0.4) {
                    LabelPosition = LabelPlacement.Right;
                } 
                else {
                    LabelPosition = LabelPlacement.Left;
                }
            }
        }
        /// <summary>
        /// on each layer the node boundary is a different polyline, smaller on the layers with large zool levels
        /// </summary>
        public Polyline BoundaryOnLayer { get; set; }

        public void Translate(Point delta) {
            GeometryNode.Center += delta;
            BoundaryOnLayer.Translate(delta);
        }
    }
}