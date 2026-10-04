using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    internal class InteractiveTangentVisibilityGraphCalculator : AlgorithmBase {
        /// <summary>
        /// the list of obstacles
        /// </summary>
        ICollection<Polygon> polygons;

        /// <summary>
        /// From these polygons we calculate visibility edges to all other polygons
        /// </summary>
        IEnumerable<Polygon> addedPolygons;

        VisibilityGraph visibilityGraph;
        List<Diagonal> diagonals;
        List<Tangent> tangents;
        RbTree<Diagonal> activeDiagonalTree;
        Polygon currentPolygon;
        ActiveDiagonalComparerWithRay activeDiagonalComparer = new ActiveDiagonalComparerWithRay();
        bool useLeftPTangents;

        /// <summary>
        /// we calculate tangents between activePolygons and between activePolygons and existingObsacles
        /// </summary>
        protected override void RunInternal() {
            useLeftPTangents = true;
            CalculateAndAddEdges();

            //use another family of tangents

            useLeftPTangents = false;
            CalculateAndAddEdges();
        }

        void CalculateAndAddEdges() {
            foreach (Polygon p in addedPolygons) {
                CalculateVisibleTangentsFromPolygon(p);
                ProgressStep();
            }
        }

        private void CalculateVisibleTangentsFromPolygon(Polygon polygon) {
            currentPolygon = polygon;
            AllocateDataStructures();
            OrganizeTangents();
            InitActiveDiagonals();
            Sweep();
        }
        
        private void AllocateDataStructures() {
            tangents = [];
            diagonals = [];
            activeDiagonalTree = new RbTree<Diagonal>(activeDiagonalComparer);
        }
      
        private void Sweep() {
            if (tangents.Count < 2)
            {
                return;
            }

            for (int i = 1; i < tangents.Count; i++) { //we processed the first element already
                Tangent t = tangents[i];
                if (t.Diagonal != null) {
                    if (t.Diagonal.RbNode == activeDiagonalTree.TreeMinimum())
                    {
                        AddVisibleEdge(t);
                    }

                    if (t.IsHigh)
                    {
                        RemoveDiagonalFromActiveNodes(t.Diagonal);
                    }
                } else {
                    if (t.IsLow) {
                        activeDiagonalComparer.PointOnTangentAndInsertedDiagonal = t.End.Point;
                        InsertActiveDiagonal(new Diagonal(t, t.Comp));
                        if (t.Diagonal.RbNode == activeDiagonalTree.TreeMinimum())
                        {
                            AddVisibleEdge(t);
                        }
                    }
                }

#if TEST_MSAGL
                //List<ICurve> cs = new List<ICurve>();

                //foreach (Diagonal d in this.activeDiagonalTree) {
                //    cs.Add(new LineSegment(d.Start, d.End));
                //}

                //foreach (Polygon p in this.polygons)
                //    cs.Add(p.Polyline);

                //cs.Add(new LineSegment(t.Start.Point, t.End.Point));
                //SugiyamaLayoutSettings.Show(cs.ToArray);
#endif
            }
        }



        private void AddVisibleEdge(Tangent t) => VisibilityGraph.AddEdge(visibilityGraph.GetVertex(t.Start), visibilityGraph.GetVertex(t.End));

        /// <summary>
        /// this function will also add the first tangent to the visible edges if needed
        /// </summary>
        private void InitActiveDiagonals() {
            if (tangents.Count == 0)
            {
                return;
            }

            Tangent firstTangent = tangents[0];
            Point firstTangentStart = firstTangent.Start.Point;
            Point firstTangentEnd = firstTangent.End.Point;

            foreach (Diagonal diagonal in diagonals) {
                if (RayIntersectDiagonal(firstTangentStart, firstTangentEnd, diagonal)) {
                    activeDiagonalComparer.PointOnTangentAndInsertedDiagonal =
                        ActiveDiagonalComparerWithRay.IntersectDiagonalWithRay(firstTangentStart, firstTangentEnd, diagonal);

                    InsertActiveDiagonal(diagonal);
                }
            }

            if (firstTangent.Diagonal.RbNode == activeDiagonalTree.TreeMinimum())
            {
                AddVisibleEdge(firstTangent);
            }

            if (firstTangent.IsLow == false) { //remove the diagonal of the top tangent from active edges 
                Diagonal diag = firstTangent.Diagonal;
                RemoveDiagonalFromActiveNodes(diag);
            }
            
        }
#if DEBUG && TEST_MSAGL

        private void AddPolylinesForShow(List<ICurve> curves) {
            foreach (Polygon p in AllObstacles)
            {
                curves.Add(p.Polyline);
            }
        }
#endif

        private void RemoveDiagonalFromActiveNodes(Diagonal diag) {
            RBNode<Diagonal> changedNode = activeDiagonalTree.DeleteSubtree(diag.RbNode);
            if (changedNode != null)
            {
                if (changedNode.Item != null)
                {
                    changedNode.Item.RbNode = changedNode;
                }
            }

            diag.LeftTangent.Diagonal = null;
            diag.RightTangent.Diagonal = null;
        }

        private void InsertActiveDiagonal(Diagonal diagonal) {
            diagonal.RbNode = activeDiagonalTree.Insert(diagonal);
            MarkDiagonalAsActiveInTangents(diagonal);
        }

        private static void MarkDiagonalAsActiveInTangents(Diagonal diagonal) {
            diagonal.LeftTangent.Diagonal = diagonal;
            diagonal.RightTangent.Diagonal = diagonal;

        }

        static bool RayIntersectDiagonal(Point pivot, Point pointOnRay, Diagonal diagonal) {
            Point a = diagonal.Start;
            Point b = diagonal.End;
            return Point.GetTriangleOrientation(pivot, a, b) == TriangleOrientation.Counterclockwise
                &&
                Point.GetTriangleOrientation(pivot, pointOnRay, a) != TriangleOrientation.Counterclockwise
                &&
                Point.GetTriangleOrientation(pivot, pointOnRay, b) != TriangleOrientation.Clockwise;
        }

        /// <summary>
        /// compare tangents by measuring the counterclockwise angle between the tangent and the edge
        /// </summary>
        /// <param name="e0"></param>
        /// <param name="e1"></param>
        /// <returns></returns>
        int TangentComparison(Tangent e0, Tangent e1) => StemStartPointComparer.CompareVectorsByAngleToXAxis(e0.End.Point - e0.Start.Point, e1.End.Point - e1.Start.Point);

        IEnumerable<Polygon> AllObstacles {
            get {
                foreach (Polygon p in addedPolygons)
                {
                    yield return p;
                }

                foreach (Polygon p in polygons)
                {
                    yield return p;
                }
            }
        }

        private void OrganizeTangents() {
            foreach (Polygon q in AllObstacles)
            {
                if (q != currentPolygon)
                {
                    ProcessPolygonQ(q);
                }
            }

            tangents.Sort(new Comparison<Tangent>(TangentComparison));
        }

        private void ProcessPolygonQ(Polygon q) {
            TangentPair tangentPair = new TangentPair(currentPolygon, q);
            if (useLeftPTangents)
            {
                tangentPair.CalculateLeftTangents();
            }
            else
            {
                tangentPair.CalculateRightTangents();
            }

            Tuple<int, int> couple = useLeftPTangents ? tangentPair.leftPLeftQ : tangentPair.rightPLeftQ;

            Tangent t0 = new Tangent(currentPolygon[couple.Item1], q[couple.Item2])
            {
                IsLow = true,
                SeparatingPolygons = !useLeftPTangents
            };
            couple = useLeftPTangents ? tangentPair.leftPRightQ : tangentPair.rightPRightQ;
            Tangent t1 = new Tangent(currentPolygon[couple.Item1], q[couple.Item2])
            {
                IsLow = false,
                SeparatingPolygons = useLeftPTangents
            };
            t0.Comp = t1;
            t1.Comp = t0;

            tangents.Add(t0);
            tangents.Add(t1);
            diagonals.Add(new Diagonal(t0, t1));
        }

        public InteractiveTangentVisibilityGraphCalculator(ICollection<Polygon> holes, IEnumerable<Polygon> addedPolygons, VisibilityGraph visibilityGraph) {
            polygons = holes;
            this.visibilityGraph = visibilityGraph;
            this.addedPolygons = addedPolygons;
        }

        internal delegate bool FilterVisibleEdgesDelegate(Point a, Point b);
    }
}
