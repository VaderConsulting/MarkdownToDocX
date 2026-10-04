using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;
using Microsoft.AGL.Core.Routing;
using Microsoft.AGL.Routing.Visibility;
using Microsoft.AGL.DebugHelpers;
using Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation;

namespace Microsoft.AGL.Routing.Spline.Bundling {
    /// <summary>
    /// Adjust current bundle-routing
    /// </summary>
    public class SimulatedAnnealing {
        /// <summary>
        /// bundle data
        /// </summary>
        readonly MetroGraphData metroGraphData;

        /// <summary>
        /// Algorithm settings
        /// </summary>
        readonly BundlingSettings bundlingSettings;

        ///  calculates rouing cost
        readonly CostCalculator costCalculator;

        ///  used for fast calculation of intersections
        readonly IntersectionCache cache;

        /// <summary>
        /// fix routing by simulated annealing algorithm
        /// </summary>
        internal static bool FixRouting(MetroGraphData metroGraphData, BundlingSettings bundlingSettings) => FixRouting(metroGraphData, bundlingSettings, null);

        internal static bool FixRouting(MetroGraphData metroGraphData, BundlingSettings bundlingSettings, HashSet<Point> changedPoints) => new SimulatedAnnealing(metroGraphData, bundlingSettings).FixRouting(changedPoints);

        SimulatedAnnealing(MetroGraphData metroGraphData, BundlingSettings bundlingSettings) {
            this.metroGraphData = metroGraphData;
            this.bundlingSettings = bundlingSettings;
            costCalculator = new CostCalculator(metroGraphData, bundlingSettings);
            cache = new IntersectionCache(metroGraphData, bundlingSettings, costCalculator, metroGraphData.Cdt);
        }

        const int MaxIterations = 100;
        const double MaxStep = 50;
        const double MinStep = 1;
        const double MinRelativeChange = 0.0005;

        HashSet<Station> stationsForOptimizations;

        /// <summary>
        /// Use constraint edge routing to reduce ink
        /// </summary>
        bool FixRouting(HashSet<Point> changedPoints) {
            stationsForOptimizations = GetStationsForOptimizations(changedPoints);

            cache.InitializeCostCache();

            double step = MaxStep;
            double energy = double.PositiveInfinity;

            List<Point> x = new List<Point>(metroGraphData.VirtualNodes().Select(v => v.Position));
            int iteration = 0;
            while (iteration++ < MaxIterations) {
                bool coordinatesChanged = TryMoveNodes();
                //TimeMeasurer.DebugOutput("  #iter = " + iteration + " moved: " + cnt + "/" + metroGraphData.VirtualNodes().Count() + " step: " + step);

                if (iteration <= 1 && !coordinatesChanged) return false;
                if (!coordinatesChanged) break;

                double oldEnergy = energy;
                energy = CostCalculator.Cost(metroGraphData, bundlingSettings);
                //TimeMeasurer.DebugOutput("energy: " + energy);

                step = UpdateMaxStep(step, oldEnergy, energy);
                List<Point> oldX = x;
                x = new List<Point>(metroGraphData.VirtualNodes().Select(v => v.Position));
                if (step < MinStep || Converged(step, oldX, x)) break;
            }

            //TimeMeasurer.DebugOutput("SA completed after " + iteration + " iterations");
            return true;
        }

        HashSet<Station> GetStationsForOptimizations(HashSet<Point> changedPoints) {
            if (changedPoints == null) {
                return new HashSet<Station>(metroGraphData.VirtualNodes());
            }
            else {
                HashSet<Station> result = [];
                foreach (Point p in changedPoints) {
                    if (metroGraphData.PointToStations.ContainsKey(p)) {
                        Station s = metroGraphData.PointToStations[p];
                        if (!s.IsRealNode) result.Add(s);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// stop SA if relative changes are small
        /// </summary>
        bool Converged(double step, List<Point> oldx, List<Point> newx) {
            //return false;

            double num = 0, den = 0;
            for (int i = 0; i < oldx.Count; i++) {
                num += (oldx[i] - newx[i]).LengthSquared;
                den += oldx[i].LengthSquared;
            }
            double res = Math.Sqrt(num / den);
            return (res < MinRelativeChange);
        }

        int stepsWithProgress = 0;

        double UpdateMaxStep(double step, double oldEnergy, double newEnergy) {
            //cooling factor
            double T = 0.8;
            if (newEnergy + 1.0 < oldEnergy) {
                stepsWithProgress++;
                if (stepsWithProgress >= 5) {
                    stepsWithProgress = 0;
                    step = Math.Min(MaxStep, step / T);
                }
            }
            else {
                stepsWithProgress = 0;
                step *= T;
            }

            return step;
        }


        bool TryMoveNodes() {
            bool coordinatesChanged = false;
            HashSet<Station> movedStations = [];
            //foreach (var node in metroGraphData.VirtualNodes()) {
            foreach (Station node in stationsForOptimizations) {
                if (TryMoveNode(node)) {
                    Debug.Assert(stationsForOptimizations.Contains(node));

                    coordinatesChanged = true;
         
                    movedStations.Add(node);
                    foreach (Station adj in node.Neighbors)
                        if (!adj.IsRealNode) movedStations.Add(adj);                    
                }
            }

            stationsForOptimizations = movedStations;
            return coordinatesChanged;
        }

        /// <summary>
        /// Move node to decrease the cost of the drawing
        /// Returns true iff position has changed
        /// </summary>
        bool TryMoveNode(Station node) {
            Point direction = BuildDirection(node);
            if (direction.Length == 0) return false;

            double stepLength = BuildStepLength(node, direction);
            if (stepLength < MinStep) {
                //try random direction
                direction = Point.RandomPoint();
                stepLength = BuildStepLength(node, direction);
                if (stepLength < MinStep)
                    return false;
            }

            Point step = direction * stepLength;
            Point newPosition = node.Position + step;
            //can this happen?
            if (metroGraphData.PointToStations.ContainsKey(newPosition)) return false;

            metroGraphData.MoveNode(node, newPosition);
            cache.UpdateCostCache(node);
            return true;
        }

        /// <summary>
        /// Calculate the direction to improve the ink function
        /// </summary>
        Point BuildDirection(Station node) {
            Point forceInk = BuildForceForInk(node);
            Point forcePL = BuildForceForPathLengths(node);
            Point forceR = BuildForceForRadius(node);
            Point forceBundle = BuildForceForBundle(node);

            Point force = forceInk + forcePL + forceR + forceBundle;
            if (force.Length < 0.1) return new Point();
            force = force.Normalize();

            return force;
        }

        double BuildStepLength(Station node, Point direction) {
            double stepLength = MinStep;

            double costGain = CostGain(node, node.Position + direction * stepLength);
            if (costGain < 0.01)
                return 0;

            while (2 * stepLength <= MaxStep) {
                double newCostGain = CostGain(node, node.Position + direction * stepLength * 2);
                if (newCostGain <= costGain)
                    break;

                stepLength *= 2;
                costGain = newCostGain;
            }

            return stepLength;
        }

        /// <summary>
        /// Computes cost delta when moving the node
        /// the cost will be negative if a new position overlaps obstacles
        /// </summary>
        double CostGain(Station node, Point newPosition) {
            double MInf = -12345678.0;
            double rGain = costCalculator.RadiusGain(node, newPosition);
            if (rGain < MInf) return MInf;
            double bundleGain = costCalculator.BundleGain(node, newPosition);
            if (bundleGain < MInf) return MInf;
            double inkGain = costCalculator.InkGain(node, newPosition);
            double plGain = costCalculator.PathLengthsGain(node, newPosition);

            return rGain + inkGain + plGain + bundleGain;
        }

        /// <summary>
        /// force to decrease ink
        /// </summary>
        Point BuildForceForInk(Station node) {
            //return new Point();
            Point direction = new Point();
            foreach (Station adj in node.Neighbors) {
                Point p = (adj.Position - node.Position);
                direction += p / p.Length;
            }

            //derivative
            Point force = direction * bundlingSettings.InkImportance;

            return force;
        }

        /// <summary>
        /// direction to decrease path lengths
        /// </summary>
        Point BuildForceForPathLengths(Station node) {
            //return new Point();
            Point direction = new Point();

            foreach (MetroNodeInfo mni in metroGraphData.MetroNodeInfosOfNode(node)) {
                Metroline metroline = mni.Metroline;
                Point u = mni.PolyPoint.Next.Point;
                Point v = mni.PolyPoint.Prev.Point;

                Point p1 = u - node.Position;
                Point p2 = v - node.Position;
                direction += p1 / (p1.Length * metroline.IdealLength);
                direction += p2 / (p2.Length * metroline.IdealLength);
            }

            //derivative
            Point force = direction * bundlingSettings.PathLengthImportance;

            return force;
        }

        /// <summary>
        /// direction to increase radii
        /// </summary>
        Point BuildForceForRadius(Station node) {
            Point direction = new Point();

            double idealR = node.cachedIdealRadius;
            bool res = metroGraphData.looseIntersections.HubAvoidsObstacles(node, node.Position, idealR, out List<Tuple<Polyline, Point>> touchedObstacles);
            Debug.Assert(res);

            foreach (Tuple<Polyline, Point> d in touchedObstacles) {
                double dist = (d.Item2 - node.Position).Length;
                Debug.Assert(dist <= idealR);
                double lforce = 2.0 * (1.0 - dist / idealR);
                Point dir = (node.Position - d.Item2).Normalize();
                direction += dir * lforce;
            }

            //derivative
            Point force = direction * bundlingSettings.HubRepulsionImportance;

            return force;
        }
        
        /// <summary>
        /// direction to push a bundle away from obstacle
        /// </summary>
        Point BuildForceForBundle(Station node) {
            Point direction = new Point();
            foreach (Station adj in node.Neighbors) {
                double idealWidth = metroGraphData.GetWidth(node, adj, bundlingSettings.EdgeSeparation);

                bool res = metroGraphData.cdtIntersections.BundleAvoidsObstacles(node, adj, node.Position, adj.Position, idealWidth / 2, out List<Tuple<Point, Point>> closestPoints);
                if (!res) {
#if DEBUG&& TEST_MSAGL
                    HubDebugger.ShowHubs(metroGraphData, bundlingSettings, new LineSegment(node.Position, adj.Position));
#endif
                }
                //Debug.Assert(res);  //todo : still unsolved

                foreach (Tuple<Point, Point> d in closestPoints) {
                    double dist = (d.Item1 - d.Item2).Length;
                    Debug.Assert(ApproximateComparer.LessOrEqual(dist, idealWidth / 2));
                    double lforce = 2.0 * (1.0 - dist / (idealWidth / 2));
                    Point dir = -(d.Item1 - d.Item2).Normalize();
                    direction += dir * lforce;
                }
            }

            //derivative
            Point force = direction * bundlingSettings.BundleRepulsionImportance;

            return force;
        }
    }
}
