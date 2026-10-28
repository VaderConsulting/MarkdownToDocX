using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MermaidRecognition.Layout.Sugiyama.Core;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Layout.Incremental;
using MermaidRecognition.Layout.Sugiyama.Layout.Layered;
using MermaidRecognition.Layout.Sugiyama.Layout.MDS;
using MermaidRecognition.Layout.Sugiyama.Routing;
using MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Initial
{
    static class InitialLayoutHelpers
    {
        internal static void RouteEdges(GeometryGraph component, LayoutAlgorithmSettings settings, CancelToken cancelToken)
        {
            EdgeRoutingMode mode = settings.EdgeRoutingSettings.EdgeRoutingMode;

            // Use straight line routing on very large graphs otherwise it is too slow 
            if (component.Nodes.Count >= 2000)
            {
                mode = EdgeRoutingMode.StraightLine;
            }

            switch (mode)
            {
                case EdgeRoutingMode.Spline:
                    SplineRouter splineRouter = new SplineRouter(
                        component,
                        settings.EdgeRoutingSettings.Padding,
                        settings.NodeSeparation, settings.EdgeRoutingSettings.ConeAngle, null);
                    splineRouter.Run(cancelToken);
                    break;
                case EdgeRoutingMode.SplineBundling:
                    splineRouter = new SplineRouter(
                        component,
                        settings.EdgeRoutingSettings.Padding,
                        settings.NodeSeparation / 20, settings.EdgeRoutingSettings.ConeAngle,
                      new BundlingSettings());
                    splineRouter.Run(cancelToken);
                    break;
                case EdgeRoutingMode.Rectilinear:
                    double edgePadding = settings.EdgeRoutingSettings.Padding;
                    double cornerRadius = settings.EdgeRoutingSettings.CornerRadius;
                    RectilinearEdgeRouter rectilinearEdgeRouter = new RectilinearEdgeRouter(component, edgePadding, cornerRadius,
                                                                          true);
                    rectilinearEdgeRouter.Run(cancelToken);
                    break;
                case EdgeRoutingMode.StraightLine:
                    StraightLineEdges router = new StraightLineEdges(component.Edges,
                                                       settings.EdgeRoutingSettings.Padding);
                    router.Run(cancelToken);
                    break;
            }
        }

        internal static void PlaceLabels(GeometryGraph component, CancelToken cancelToken)
        {
            List<Label> labelList = [.. component.Edges.SelectMany(e => e.Labels)];
            EdgeLabelPlacement labelPlacer = new EdgeLabelPlacement(component, labelList);
            labelPlacer.Run(cancelToken);
        }

        internal static void FixBoundingBox(GeometryGraph component, LayoutAlgorithmSettings settings)
        {
            // Pad the graph with margins so the packing will be spaced out.
            component.Margins = settings.ClusterMargin;
            component.UpdateBoundingBox();

            // Zero the graph
            component.Translate(-component.BoundingBox.LeftBottom);
        }
    }
}
