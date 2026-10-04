using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.DebugHelpers;
using Microsoft.AGL.Core.Routing;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    /// <summary>
    /// this class nudges the edges
    /// </summary>
    public class HubDebugger {
        
#if DEBUG && TEST_MSAGL
        readonly MetroGraphData mgd;
        readonly BundlingSettings bundlingSettings;
        internal static void ShowHubs(MetroGraphData mgd, BundlingSettings bundlingSettings, params ICurve[] iCurves
            ) {
            HubDebugger hd = new HubDebugger(mgd, bundlingSettings);
            if (iCurves != null) {
                List<DebugCurve> dc = hd.CreateDebugCurves(iCurves);
                LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(dc);
            }
        }

        internal static void ShowHubs(MetroGraphData mgd, BundlingSettings bundlingSettings, bool withIdeal) {
            if (!withIdeal)
            {
                return;
            }

            foreach (Station v in mgd.Stations) {
                ShowHubs(mgd, bundlingSettings, v);
            }
        }

        internal static void ShowHubs(MetroGraphData mgd, BundlingSettings bundlingSettings, Station highlightedNode) {
            HubDebugger hd = new HubDebugger(mgd, bundlingSettings);
            List<DebugCurve> debugCurves = hd.CreateDebugCurves();
            debugCurves.Add(new DebugCurve(100,1, "magenta", CurveFactory.CreateCircle(3, highlightedNode.Position)));
            debugCurves.Add(new DebugCurve(100, 0.1, "green", highlightedNode.BoundaryCurve));

            Console.WriteLine(highlightedNode.SerialNumber);
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debugCurves);
        }

        internal HubDebugger(MetroGraphData mgd, BundlingSettings bundlingSettings) {
            this.mgd = mgd;
            this.bundlingSettings = bundlingSettings;
        }

        internal List<DebugCurve> CreateDebugCurves(params ICurve[] iCurves) {
            List<DebugCurve> curves =
            [
                .. Obstacles(),
                //curves.AddRange(BBoxes());
                .. Hubs(),
                .. Paths(),
            ];
            //curves.AddRange(Bundles());
            //curves.AddRange(IdealHubs());
            //curves.AddRange(IdealHubsWithNeighbors());
            //curves.AddRange(IdealBundles());
            if (iCurves != null) {
                int i = 0;
                foreach(ICurve iCurve in iCurves)
                {
                    if (iCurve != null)
                    {
                        curves.Add(new DebugCurve(100,0.2, DebugCurve.Colors[i++], iCurve));
                    }
                }
            }
            return curves;
        }

        IEnumerable<DebugCurve> Obstacles() => mgd.TightTree.GetAllLeaves().Select(n => new DebugCurve(100, 0.5, "black", n)).Concat(
                    mgd.LooseTree.GetAllLeaves().Select(n => new DebugCurve(100, 0.1, "green", n)));

        IEnumerable<DebugCurve> BBoxes() => mgd.Stations.Where(n => n.IsRealNode).Select(n => new DebugCurve("red", CurveFactory.CreateCircle(n.BoundaryCurve.BoundingBox.Diagonal / 2, n.Position)));

        IEnumerable<DebugCurve> GraphNodes() {
            Set<ICurve> nodes = new Set<ICurve>(mgd.Edges.Select(e => e.SourcePort.Curve).Concat(mgd.Edges.Select(e => e.TargetPort.Curve)));
            return nodes.Select(n => new DebugCurve(100, 1, "black", n));
        }

        IEnumerable<DebugCurve> Hubs() => mgd.Stations.Select(station => new DebugCurve(100, 1, "blue", CurveFactory.CreateCircle(Math.Max(station.Radius, 1.0), station.Position)));

        IEnumerable<DebugCurve> IdealHubs() => mgd.VirtualNodes().Select(station => new DebugCurve(100, 1, "black",
                CurveFactory.CreateCircle(HubRadiiCalculator.CalculateIdealHubRadiusWithNeighbors(mgd, bundlingSettings, station), station.Position)));

        IEnumerable<DebugCurve> IdealHubsWithNeighbors() => mgd.VirtualNodes().Select(station => new DebugCurve(200, 1, "black",
                CurveFactory.CreateCircle(HubRadiiCalculator.CalculateIdealHubRadiusWithNeighbors(mgd, bundlingSettings, station), station.Position)));

        IEnumerable<DebugCurve> Bundles() => mgd.VirtualEdges().Select(pr => new DebugCurve(100, 1, "red", new LineSegment(pr.Item1.Position, pr.Item2.Position)));

        IEnumerable<DebugCurve> IdealBundles() {
            List<DebugCurve> dc = [];
            foreach (Tuple<Station, Station> edge in mgd.VirtualEdges()) {
                Station node = edge.Item1;
                Station adj = edge.Item2;

                double width = mgd.GetWidth(node, adj, bundlingSettings.EdgeSeparation);
                dc.Add(new DebugCurve(0.1, "black", Intersections.Create4gon(node.Position, adj.Position, width, width)));
            }

            return dc;
        }

        IEnumerable<DebugCurve> Paths() {
            List<DebugCurve> dc = [];
            foreach (Metroline metroline in mgd.Metrolines) {
                dc.Add(new DebugCurve(100, 1, "black", metroline.Polyline));
            }

            return dc;
        }
#endif

        /*void SaveAsBitmap(string filename, GViewer gViewer) {
            int w = (int)Math.Ceiling((double) gViewer.Graph.Width);
            int h = (int)Math.Ceiling((double) gViewer.Graph.Height);

            Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
                DrawGeneral(w, h, graphics, gViewer);

            bitmap.Save(filename);
        }

        void DrawGeneral(int w, int h, Graphics graphics, GViewer gViewer) {
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            DrawAll(w, h, graphics, gViewer);
        }

        void DrawAll(int w, int h, Graphics graphics, GViewer gViewer) {
            //fill the whole image
            graphics.FillRectangle(new SolidBrush(Draw.MsaglColorToDrawingColor(gViewer.Graph.Attr.BackgroundColor)),
                                   new RectangleF(0, 0, w, h));

            //calculate the transform
            double s = 1.0;
            Graph g = gViewer.Graph;
            double x = 0.5 * w - s * (g.Left + 0.5 * g.Width);
            double y = 0.5 * h + s * (g.Bottom + 0.5 * g.Height);

            graphics.Transform = new Matrix((float)s, 0, 0, (float)-s, (float)x, (float)y);
            Draw.DrawPrecalculatedLayoutObject(graphics, gViewer.DGraph);
        } */

    }
}