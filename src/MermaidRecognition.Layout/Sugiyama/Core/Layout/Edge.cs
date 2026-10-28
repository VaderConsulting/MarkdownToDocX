using System;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout {
    /// <summary>
    /// Edge of the graph
    /// </summary>
#if TEST_MSAGL

#endif
    public class Edge : GeometryObject, ILabeledObject {
        /// <summary>
        /// Defines the way the edge connects to the source.
        /// The member is used at the moment only when adding an edge to the graph.
        /// </summary>
        public Port? SourcePort { get => EdgeGeometry.SourcePort; set => EdgeGeometry.SourcePort = value;
        }

        /// <summary>
        /// defines the way the edge connects to the target
        /// The member is used at the moment only when adding an edge to the graph.
        /// </summary>
        public Port? TargetPort { get => EdgeGeometry.TargetPort; set => EdgeGeometry.TargetPort = value;
        }

        readonly List<Label> labels = [];

        /// <summary>
        /// gets the default (first) label of the edge
        /// </summary>
        public Label Label {
            get{
                if (labels.Count == 0)
                {
                    return null;
                }

                return labels[0];
            }
            set {
                if (labels.Count == 0) {
                    labels.Add(value);
                } else {
                    labels[0] = value;
                }
            }
        }
        /// <summary>
        /// Returns the full enumeration of labels associated with this edge
        /// </summary>
        public IList<Label> Labels => labels;

        Node source;

        /// <summary>
        /// id of the source node
        /// </summary>
        public Node Source { get => source; set => source = value;
        }

        int sourceAnchorNumber;

        /// <summary>
        /// For future use: The index of the anchor at the source node. By default is zero.
        /// </summary>
        public int SourceAnchorNumber { get => sourceAnchorNumber; set => sourceAnchorNumber = value;
        }

        Node target;

        /// <summary>
        /// id of the target node
        /// </summary>
        public Node Target { get => target; set => target = value;
        }


        int targetAnchorNumber;

        /// <summary>
        /// For future use:The index of the anchor at the target node. By default is zero.
        /// </summary>
        public int TargetAnchorNumber { get => targetAnchorNumber; set => targetAnchorNumber = value;
        }

        /// <summary>
        /// Label width, need to backup it for transformation purposes
        /// </summary>
        internal double OriginalLabelWidth { get; set; }

        /// <summary>
        /// Original label height
        /// </summary>
        internal double OriginalLabelHeight { get; set; }

        /// <summary>
        /// Edge constructor
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="labelWidth"></param>
        /// <param name="labelHeight"></param>
        /// <param name="edgeThickness"></param>
        public Edge(Node source, Node target, double labelWidth, double labelHeight, double edgeThickness) {
            this.source = source;
            this.target = target;
            if (labelWidth > 0)
            {
                Label = new Label(labelWidth, labelHeight, this);
            }

            LineWidth = edgeThickness;
        }

        /// <summary>
        /// Constructs an edge without a label or arrowheads and with edge thickness 1.
        /// </summary>
        /// <param name="source">souce node</param>
        /// <param name="target">target node</param>
        public Edge(Node source, Node target)
            : this(source, target, 0, 0, 1) {
        }

        /// <summary>
        /// The default constructor
        /// </summary>
        public Edge() : this(null, null) {
        }

        /// <summary>
        /// The label bounding box
        /// </summary>
        internal Rectangle LabelBBox => Label.BoundingBox;

        double length = 1;

        /// <summary>
        /// applicable for MDS layouts
        /// </summary>
        public double Length { get => length; set => length = value;
        }

        int weight = 1;

        /// <summary>
        /// The greater is the weight the more important is keeping the edge short. It is 1 by default.
        /// Other values are not tested yet.
        /// </summary>
        public int Weight { get => weight; set => weight = value;
        }

        int separation = 1;

        /// <summary>
        /// The minimum number of levels dividing source from target: 1 means that the edge goes down at least one level.
        /// Separation is 1 by default. Other values are not tested yet.
        /// </summary>
        public int Separation { get => separation; set => separation = value;
        }


        /// <summary>
        /// overrides ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString() => source + "->" + target;

        /// <summary>
        /// edge thickness
        /// </summary>
        public double LineWidth { get => EdgeGeometry.LineWidth; set => EdgeGeometry.LineWidth = value;
        }

        /// <summary>
        /// The bounding box of the edge curve
        /// </summary>
        public override Rectangle BoundingBox
        {
            get
            {

                Rectangle rect = Rectangle.CreateAnEmptyBox();
                if (UnderlyingPolyline != null)
                {
                    foreach (Point p in UnderlyingPolyline)
                    {
                        rect.Add(p);
                    }
                }

                if (Curve != null)
                {
                    rect.Add(Curve.BoundingBox);
                }

                if (EdgeGeometry != null)
                {
                    if (EdgeGeometry.SourceArrowhead != null)
                    {
                        rect.Add(EdgeGeometry.SourceArrowhead.TipPosition);
                    }

                    if (EdgeGeometry.TargetArrowhead != null)
                    {
                        rect.Add(EdgeGeometry.TargetArrowhead.TipPosition);
                    }
                }

                double del = LineWidth;
                rect.Left -= del;
                rect.Top += del;
                rect.Right += del;
                rect.Bottom -= del;
                return rect;
            }

            set => throw new NotImplementedException();
        }


        EdgeGeometry edgeGeometry = new EdgeGeometry();

        /// <summary>
        /// Gets or sets the edge geometry: the curve, the arrowhead positions and the underlying polyline
        /// </summary>
        public EdgeGeometry EdgeGeometry { get => edgeGeometry; set => edgeGeometry = value;
        }

        /// <summary>
        /// the polyline of the untrimmed spline
        /// </summary>
        public SmoothedPolyline UnderlyingPolyline { get => edgeGeometry.SmoothedPolyline; set => edgeGeometry.SmoothedPolyline = value;
        }

        /// <summary>
        /// A curve representing the edge
        /// </summary>
        public ICurve Curve
        {
            get => edgeGeometry?.Curve;
            set
            {
                RaiseLayoutChangeEvent(value);
                edgeGeometry.Curve = value;
            }
        }

        /// <summary>
        /// Transform the curve, arrowheads and label according to the given matrix
        /// </summary>
        /// <param name="matrix">affine transform matrix</param>
        internal void Transform(PlaneTransformation matrix)
        {
            if (Curve == null)
            {
                return;
            }

            Curve = Curve.Transform(matrix);
            if (UnderlyingPolyline != null)
            {
                for (Site s = UnderlyingPolyline.HeadSite, s0 = UnderlyingPolyline.HeadSite;
                     s != null;
                     s = s.Next, s0 = s0.Next)
                {
                    s.Point = matrix * s.Point;
                }
            }

            Arrowhead? sourceArrow = edgeGeometry.SourceArrowhead;
            sourceArrow?.TipPosition = matrix * sourceArrow.TipPosition;

            Arrowhead? targetArrow = edgeGeometry.TargetArrowhead;
            targetArrow?.TipPosition = matrix * targetArrow.TipPosition;

            Label?.Center = matrix * LabelBBox.Center;
        }

        /// <summary>
        /// Translate the edge curve arrowheads and label by the specified delta
        /// </summary>
        /// <param name="delta">amount to shift geometry</param>
        public void Translate(Point delta)
        {
            EdgeGeometry?.Translate(delta);
            foreach (Label l in Labels)
            {
                l.Translate(delta);
            }
        }

		/// <summary>
		/// transforms relative to given rectangles
		/// </summary>
		public void TransformRelativeTo(Rectangle oldBounds, Rectangle newBounds)
        {
            if (EdgeGeometry != null) {
                PlaneTransformation toOrigin = new PlaneTransformation(1, 0, -oldBounds.Left, 0, 1, -oldBounds.Bottom);
                PlaneTransformation scale = new PlaneTransformation(newBounds.Width/oldBounds.Width, 0, 0,
                                                    0,newBounds.Height/oldBounds.Height, 0);
                PlaneTransformation toNewBounds = new PlaneTransformation(1, 0, newBounds.Left, 0, 1, newBounds.Bottom);
                Transform(toNewBounds*scale*toOrigin);
            }
            foreach (Label l in Labels)
            {
                l.Translate(newBounds.LeftBottom - oldBounds.LeftBottom);
            }
        }

        /// <summary>
        /// Checks if an arrowhead is needed at the source
        /// </summary>
        public bool ArrowheadAtSource => EdgeGeometry != null && EdgeGeometry.SourceArrowhead != null;

        /// <summary>
        /// Checks if an arrowhead is needed at the target
        /// </summary>
        public bool ArrowheadAtTarget => EdgeGeometry != null && EdgeGeometry.TargetArrowhead != null;

        /// <summary>
        /// Routes a self edge inside the given "howMuchToStickOut" parameter
        /// </summary>
        /// <param name="boundaryCurve"></param>
        /// <param name="howMuchToStickOut"></param>
        /// <param name="smoothedPolyline"> the underlying polyline used later for editing</param>
        /// <returns></returns>
        internal static ICurve RouteSelfEdge(ICurve boundaryCurve, double howMuchToStickOut, out SmoothedPolyline smoothedPolyline)
        {
            //we just need to find the box of the corresponding node
            double w = boundaryCurve.BoundingBox.Width;
            double h = boundaryCurve.BoundingBox.Height;
            Point center = boundaryCurve.BoundingBox.Center;

            Point p0 = new Point(center.X - (w / 4), center.Y);
            Point p1 = new Point(center.X - (w / 4), center.Y - (h / 2) - howMuchToStickOut);
            Point p2 = new Point(center.X + (w / 4), center.Y - (h / 2) - howMuchToStickOut);
            Point p3 = new Point(center.X + (w / 4), center.Y);

            smoothedPolyline = SmoothedPolyline.FromPoints([p0, p1, p2, p3]);

            return smoothedPolyline.CreateCurve();
        }

        /// <summary>
        /// Routes a self edge inside the given "howMuchToStickOut" parameter
        /// </summary>
        /// <param name="boundaryCurve"></param>
        /// <param name="howMuchToStickOut"></param>
        /// <returns></returns>
        internal static ICurve RouteSelfEdgeAtSide(ICurve boundaryCurve, double howMuchToStickOut)
        {
            //we just need to find the box of the corresponding node
            double w = boundaryCurve.BoundingBox.Width;
            double h = boundaryCurve.BoundingBox.Height;
            Point center = boundaryCurve.BoundingBox.Center;

            Point p0 = new Point(center.X, center.Y - (h / 4));
            Point p1 = new Point(center.X - (w / 2) - howMuchToStickOut, center.Y - (h / 4));
            Point p2 = new Point(center.X - (w / 2) - howMuchToStickOut, center.Y);
            Point p3 = new Point(center.X - (w / 2) - howMuchToStickOut, center.Y + (h / 4));
            Point p4 = new Point(center.X, center.Y + (h / 4));

            Curve curve = new Curve();
            curve.AddSegment(new CubicBezierSegment(p0, p1, p1, p2));
            curve.AddSegment(new CubicBezierSegment(p2, p3, p3, p4));
            return curve;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newValue"></param>
        public override void RaiseLayoutChangeEvent(object newValue) => edgeGeometry.RaiseLayoutChangeEvent(newValue);


        /// <summary>
        /// 
        /// </summary>
        public override event EventHandler<LayoutChangeEventArgs> BeforeLayoutChangeEvent { add => edgeGeometry.LayoutChangeEvent += value; remove => edgeGeometry.LayoutChangeEvent -= value;
        }

        internal bool UnderCollapsedCluster() => Source.UnderCollapsedCluster() || Target.UnderCollapsedCluster();
    }
}