using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AGL.Core;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.GraphAlgorithms;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Core.ProjectionSolver;
using Microsoft.AGL.Routing;

namespace Microsoft.AGL.Layout.Layered {
    internal class TwoLayerFlatEdgeRouter : AlgorithmBase {
        readonly int[] bottomLayer;
        InteractiveEdgeRouter interactiveEdgeRouter;
        double[] labelCenters;
        Dictionary<Label, ICurve> labelsToLabelObstacles = [];
        IntPair[] pairArray;
        readonly Routing routing;
        readonly int[] topLayer;
        private SugiyamaLayoutSettings settings;

        internal TwoLayerFlatEdgeRouter(SugiyamaLayoutSettings settings, Routing routing, int[] bottomLayer, int[] topLayer)
        {
            this.settings = settings;
            this.topLayer = topLayer;
            this.bottomLayer = bottomLayer;
            this.routing = routing;
            InitLabelsInfo();
        }

        Database Database => routing.Database;

        int[] Layering => routing.LayerArrays.Y;

        BasicGraph<IntEdge> IntGraph => routing.IntGraph;

        LayerArrays LayerArrays => routing.LayerArrays;


        double PaddingForEdges => settings.LayerSeparation / 8;

        void InitLabelsInfo() {
            pairArray = new Set<IntPair>(from v in bottomLayer
                                         where v < IntGraph.NodeCount
                                         from edge in IntGraph.OutEdges(v)
                                         where edge.Source != edge.Target
                                         where Layering[edge.Target] == Layering[edge.Source]
                                         select new IntPair(edge.Source, edge.Target)).ToArray();
            labelCenters = new double[pairArray.Length];
            int i = 0;
            foreach (IntPair p in pairArray) {
                int leftNode, rightNode;
                if (LayerArrays.X[p.First] < LayerArrays.X[p.Second]) {
                    leftNode = p.First;
                    rightNode = p.Second;
                } else {
                    leftNode = p.Second;
                    rightNode = p.First;
                }
                labelCenters[i++] = (Database.Anchors[leftNode].Right + Database.Anchors[rightNode].Left)/2;
                //labelCenters contains ideal position for nodes at the moment
            }
            InitLabelsToLabelObstacles();
        }

        void InitLabelsToLabelObstacles() {
            labelsToLabelObstacles = [];
            IEnumerable<Label> labels = from p in pairArray from label in PairLabels(p) select label;
            foreach (Label label in labels)
            {
                labelsToLabelObstacles[label] = CreatObstaceOnLabel(label);
            }
        }

        double GetMaxLabelWidth(IntPair intPair) {
            IEnumerable<Label> multiEdgeLabels = PairLabels(intPair);

            if (multiEdgeLabels.Any())
            {
                return multiEdgeLabels.Max(label => label.Width);
            }

            return 0;
        }

        IEnumerable<Label> PairLabels(IntPair intPair) => from edge in Database.GetMultiedge(intPair)
                                                          let label = edge.Edge.Label
                                                          where label != null
                                                          select label;

        /// <summary>
        /// Executes the algorithm.
        /// </summary>
        protected override void RunInternal() {
            if (pairArray.Length > 0) {
                PositionLabelsOfFlatEdges();
                interactiveEdgeRouter = new InteractiveEdgeRouter(GetObstacles(), PaddingForEdges, PaddingForEdges/3, Math.PI/6);
                foreach (IntEdge intEdge in IntEdges())
                {
                    ProgressStep();
                    RouteEdge(intEdge);
                }
            }
        }

        IEnumerable<ICurve> GetObstacles() => (from v in topLayer.Concat(bottomLayer)
                                               where v < routing.OriginalGraph.Nodes.Count
                                               select routing.IntGraph.Nodes[v].BoundaryCurve).Concat(LabelCurves());

        IEnumerable<ICurve> LabelCurves() => from edge in IntEdges()
                                             let label = edge.Edge.Label
                                             where label != null
                                             select CreatObstaceOnLabel(label);

        static ICurve CreatObstaceOnLabel(Label label) {
            Curve c = new Curve();
            double obstacleBottom = label.Center.Y - label.Height/4;
            c.AddSegment(new LineSegment(new Point(label.BoundingBox.Left, obstacleBottom),
                                         new Point(label.BoundingBox.Right, obstacleBottom)));
            Curve.ContinueWithLineSegment(c, label.BoundingBox.RightTop);
            Curve.ContinueWithLineSegment(c, label.BoundingBox.LeftTop);
            Curve.CloseCurve(c);
            return c;
        }

        IEnumerable<IntEdge> IntEdges() => from pair in pairArray from edge in Database.GetMultiedge(pair) select edge;

        void RouteEdge(IntEdge edge) {
            if (edge.HasLabel)
            {
                RouteEdgeWithLabel(edge, edge.Edge.Label);
            }
            else
            {
                RouteEdgeWithNoLabel(edge);
            }
        }

        void RouteEdgeWithLabel(IntEdge intEdge, Label label) {
            //we allow here for the edge to cross its own label
            Node sourceNode = routing.IntGraph.Nodes[intEdge.Source];
            Node targetNode = routing.IntGraph.Nodes[intEdge.Target];
            FloatingPort sourcePort = new FloatingPort(sourceNode.BoundaryCurve, sourceNode.Center);
            FloatingPort targetPort = new FloatingPort(targetNode.BoundaryCurve, targetNode.Center);
            ICurve labelObstacle = labelsToLabelObstacles[label];
            FloatingPort labelPort = new FloatingPort(labelObstacle, label.Center);
            interactiveEdgeRouter.RouteSplineFromPortToPortWhenTheWholeGraphIsReady(sourcePort, labelPort, true, out SmoothedPolyline poly0);
            interactiveEdgeRouter.RouteSplineFromPortToPortWhenTheWholeGraphIsReady(labelPort, targetPort, true, out SmoothedPolyline poly1);
            Site site = poly1.HeadSite.Next;

            Site lastSite = poly0.LastSite;
            lastSite.Next = site;
            site.Previous = lastSite;
            EdgeGeometry eg = intEdge.Edge.EdgeGeometry;
            eg.SetSmoothedPolylineAndCurve(poly0);
            Arrowheads.TrimSplineAndCalculateArrowheads(eg,  intEdge.Edge.Source.BoundaryCurve,
                                                             intEdge.Edge.Target.BoundaryCurve, eg.Curve, false, 
                                                             settings.EdgeRoutingSettings.KeepOriginalSpline);
        }

        void RouteEdgeWithNoLabel(IntEdge intEdge) {
            Node sourceNode = routing.IntGraph.Nodes[intEdge.Source];
            Node targetNode = routing.IntGraph.Nodes[intEdge.Target];
            FloatingPort sourcePort = new FloatingPort(sourceNode.BoundaryCurve, sourceNode.Center);
            FloatingPort targetPort = new FloatingPort(targetNode.BoundaryCurve, targetNode.Center);
            EdgeGeometry eg = intEdge.Edge.EdgeGeometry;
            eg.Curve = interactiveEdgeRouter.RouteSplineFromPortToPortWhenTheWholeGraphIsReady(sourcePort, targetPort, true, out SmoothedPolyline sp);
            Arrowheads.TrimSplineAndCalculateArrowheads(eg,  intEdge.Edge.Source.BoundaryCurve,
                                                             intEdge.Edge.Target.BoundaryCurve, eg.Curve, false, 
                                                             settings.EdgeRoutingSettings.KeepOriginalSpline);
            intEdge.Edge.EdgeGeometry = eg;
        }

        void PositionLabelsOfFlatEdges() {
            if (labelCenters == null || labelCenters.Length == 0)
            {
                return;
            }

            SortLabelsByX();
            CalculateLabelsX();
        }

        void CalculateLabelsX() {
            int i;
            ISolverShell solver = ConstrainedOrdering.CreateSolver();
            for (i = 0; i < pairArray.Length; i++)
            {
                solver.AddVariableWithIdealPosition(i, labelCenters[i], GetLabelWeight(pairArray[i]));
            }

            //add non overlapping constraints between to neighbor labels
            double prevLabelWidth = GetMaxLabelWidth(pairArray[0]);
            for (i = 0; i < pairArray.Length - 1; i++)
            {
                solver.AddLeftRightSeparationConstraint(i, i + 1,
                                                        (prevLabelWidth +
                                                         (prevLabelWidth = GetMaxLabelWidth(pairArray[i + 1])))/2 +
                                                        settings.NodeSeparation);
            }

            for (i = 0; i < labelCenters.Length; i++) {
                double x = labelCenters[i] = solver.GetVariableResolvedPosition(i);
                foreach (Label label in PairLabels(pairArray[i]))
                {
                    label.Center = new Point(x, label.Center.Y);
                }
            }
        }

        double GetLabelWeight(IntPair intPair) => Database.GetMultiedge(intPair).Count;

        void SortLabelsByX() => Array.Sort(labelCenters, pairArray);
    }
}