using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation;
using System;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    /// <summary>
    /// Calculates the cost of the routing
    /// </summary>
    internal class CostCalculator {
        internal const double Inf = 1000000000.0;

        readonly MetroGraphData metroGraphData;
        readonly BundlingSettings bundlingSettings;

        internal CostCalculator(MetroGraphData metroGraphData, BundlingSettings bundlingSettings) {
            this.metroGraphData = metroGraphData;
            this.bundlingSettings = bundlingSettings;
        }

        /// <summary>
        /// Error of ink
        /// </summary>
        static internal double InkError(double oldInk, double newInk, BundlingSettings bundlingSettings) => (oldInk - newInk) * bundlingSettings.InkImportance;

        /// <summary>
        /// Error of path lengths
        /// </summary>
        static internal double PathLengthsError(double oldLength, double newLength, double idealLength, BundlingSettings bundlingSettings) => (oldLength - newLength) * (bundlingSettings.PathLengthImportance / idealLength);

        /// <summary>
        /// Error of hubs
        /// </summary>
        static internal double RError(double idealR, double nowR, BundlingSettings bundlingSettings) {
            if (idealR <= nowR)
            {
                return 0;
            }

            double res = bundlingSettings.HubRepulsionImportance * (1.0 - nowR / idealR) * (idealR - nowR);
            return res;
        }

        /// <summary>
        /// Error of bundles
        /// </summary>
        static internal double BundleError(double idealWidth, double nowWidth, BundlingSettings bundlingSettings) {
            if (idealWidth <= nowWidth)
            {
                return 0;
            }

            double res = bundlingSettings.BundleRepulsionImportance * (1.0 - nowWidth / idealWidth) * (idealWidth - nowWidth);
            return res;
        }

        /// <summary>
        /// Cost of the whole graph
        /// </summary>
        static internal double Cost(MetroGraphData metroGraphData, BundlingSettings bundlingSettings) {
            double cost = 0;

            //ink
            cost += bundlingSettings.InkImportance * metroGraphData.Ink;

            //path lengths
            foreach (Metroline metroline in metroGraphData.Metrolines) {
                cost += bundlingSettings.PathLengthImportance * metroline.Length / metroline.IdealLength;
            }

            cost += CostOfForces(metroGraphData, bundlingSettings);

            return cost;
        }

        /// <summary>
        /// Cost of the whole graph (hubs and bundles)
        /// </summary>
        static internal double CostOfForces(MetroGraphData metroGraphData, BundlingSettings bundlingSettings) {
            double cost = 0;

            //hubs
            foreach (Station v in metroGraphData.VirtualNodes()) {
                cost += v.cachedRadiusCost;
            }

            //bundles
            foreach (Tuple<Station, Station> edge in metroGraphData.VirtualEdges()) {
                Station v = edge.Item1;
                Station u = edge.Item2;
                cost += metroGraphData.GetIjInfo(v, u).cachedBundleCost;
            }

            return cost;
        }

        /// <summary>
        /// Gain of 'ink' function
        /// </summary>
        internal double QGain(Station node, Point newPosition) => InkGain(node, newPosition) + PathLengthsGain(node, newPosition);

        /// <summary>
        /// Gain of ink
        /// </summary>
        internal double InkGain(Station node, Point newPosition) {
            //ink
            double oldInk = metroGraphData.Ink;
            double newInk = metroGraphData.Ink;
            foreach (Station adj in node.Neighbors) {
                Point adjPosition = adj.Position;
                newInk -= (adjPosition - node.Position).Length;
                newInk += (adjPosition - newPosition).Length;
            }
            return InkError(oldInk, newInk, bundlingSettings);
        }

        /// <summary>
        /// Gain of path lengths
        /// </summary>
        internal double PathLengthsGain(Station node, Point newPosition) {
            double gain = 0;
            //edge lengths
            foreach (MetroNodeInfo e in metroGraphData.MetroNodeInfosOfNode(node)) {
                double oldLength = e.Metroline.Length;
                double newLength = e.Metroline.Length;

                Point prev = e.PolyPoint.Prev.Point;
                Point next = e.PolyPoint.Next.Point;

                newLength += (next - newPosition).Length + (prev - newPosition).Length - (next - node.Position).Length - (prev - node.Position).Length;

                gain += PathLengthsError(oldLength, newLength, e.Metroline.IdealLength, bundlingSettings);
            }

            return gain;
        }

        /// <summary>
        /// Gain of radii
        /// </summary>
        internal double RadiusGain(Station node, Point newPosition) {
            double gain = 0;

            gain += node.cachedRadiusCost;
            gain -= RadiusCost(node, newPosition);

            return gain;
        }

        internal double RadiusCost(Station node, Point newPosition) {
            double idealR;
            if (ApproximateComparer.Close(node.Position, newPosition))
            {
                idealR = node.cachedIdealRadius;
            }
            else
            {
                idealR = HubRadiiCalculator.CalculateIdealHubRadiusWithNeighbors(metroGraphData, bundlingSettings, node, newPosition);
            }

            if (!metroGraphData.looseIntersections.HubAvoidsObstacles(node, newPosition, idealR, out List<Tuple<Polyline, Point>>? touchedObstacles))
            {
                return Inf;
            }

            double cost = 0;
            foreach (Tuple<Polyline, Point> d in touchedObstacles) {
                double dist = (d.Item2 - newPosition).Length;
                cost += RError(idealR, dist, bundlingSettings);
            }

            return cost;
        }

        /// <summary>
        /// Gain of bundles
        /// if a newPosition is not valid (e.g. intersect obstacles) the result is -inf
        /// </summary>
        internal double BundleGain(Station node, Point newPosition) {
            double gain = 0;

            gain += node.cachedBundleCost;
            foreach (Station adj in node.Neighbors) {
                double lgain = BundleCost(node, adj, newPosition);
                if (ApproximateComparer.GreaterOrEqual(lgain, Inf))
                {
                    return -Inf;
                }

                gain -= lgain;
            }

            return gain;
        }

        internal double BundleCost(Station node, Station adj, Point newPosition) {
            double idealWidth = metroGraphData.GetWidth(node, adj, bundlingSettings.EdgeSeparation);

            double cost = 0;
            //find conflicting obstacles
            if (!metroGraphData.cdtIntersections.BundleAvoidsObstacles(node, adj, newPosition, adj.Position, idealWidth, out List<Tuple<Point, Point>>? closestDist)) {
                return Inf;
            }

            foreach (Tuple<Point, Point> pair in closestDist) {
                double dist = (pair.Item1 - pair.Item2).Length;
                cost += BundleError(idealWidth / 2, dist, bundlingSettings);
            }

            return cost;
        }

    }
}
