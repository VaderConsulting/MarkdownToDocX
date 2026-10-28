using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation {
    internal class EdgeInserter(CdtEdge edge, Set<CdtTriangle> triangles, RbTree<CdtFrontElement> front, Func<CdtSite, CdtSite, CdtEdge> createEdgeDelegate)
    {
        readonly CdtEdge edge = edge;
        readonly Set<CdtTriangle> triangles = triangles;
        readonly RbTree<CdtFrontElement> front = front;
        readonly Func<CdtSite, CdtSite, CdtEdge> createEdgeDelegate = createEdgeDelegate;
        List<CdtSite> rightPolygon = [];
        List<CdtSite> leftPolygon = [];
        List<CdtTriangle> addedTriangles=[];

        public void Run() {
            TraceEdgeThroughTriangles();
            TriangulatePolygon(rightPolygon, edge.upperSite, edge.lowerSite, true);
            TriangulatePolygon(leftPolygon, edge.upperSite, edge.lowerSite, false);
            UpdateFront();
        }

        void UpdateFront() {
            Set<CdtEdge> newFrontEdges = [];
            foreach (CdtTriangle t in addedTriangles)
            {
                foreach (CdtEdge e in t.Edges)
                {
                    if (e.CwTriangle == null || e.CcwTriangle == null)
                    {
                        newFrontEdges.Insert(e);
                    }
                }
            }

            foreach (CdtEdge e in newFrontEdges)
            {
                AddEdgeToFront(e);
            }
        }

        void AddEdgeToFront(CdtEdge e) {
            CdtSite leftSite =e.upperSite.Point.X<e.lowerSite.Point.X?e.upperSite:e.lowerSite;
            front.Insert(new CdtFrontElement(leftSite, e));
        }

        void TriangulatePolygon(List<CdtSite> polygon, CdtSite a, CdtSite b, bool reverseTrangleWhenCompare) {
            if (polygon.Count > 0)
            {
                TriangulatePolygon(0, polygon.Count - 1, polygon, a, b,reverseTrangleWhenCompare);
            }
        }

        void TriangulatePolygon(int start, int end, List<CdtSite> polygon, CdtSite a, CdtSite b, bool reverseTrangleWhenCompare) {
            //            if(CdtSweeper.db)
            //               CdtSweeper.ShowFront(triangles,front, Enumerable.Range(start, end-start+1).Select(i=> new Ellipse(10,10,polygon[i].Point)).ToArray(), new[]{new LineSegment(a.Point,b.Point)});
            CdtSite c = polygon[start];
            int cIndex = start;
            for (int i = start + 1; i <= end; i++) {
                CdtSite v = polygon[i];
                if (LocalInCircle(v, a, b, c, reverseTrangleWhenCompare)) {
                    cIndex = i;
                    c = v;
                }
            }
            CdtTriangle t = new CdtTriangle(a, b, c, createEdgeDelegate);
            triangles.Insert(t);
            addedTriangles.Add(t);
            if (start < cIndex)
            {
                TriangulatePolygon(start, cIndex - 1, polygon, a, c, reverseTrangleWhenCompare);
            }

            if (cIndex < end)
            {
                TriangulatePolygon(cIndex + 1, end, polygon, c, b, reverseTrangleWhenCompare);
            }
        }

        static bool LocalInCircle(CdtSite v, CdtSite a, CdtSite b, CdtSite c, bool reverseTrangleWhenCompare) => reverseTrangleWhenCompare ? CdtSweeper.InCircle(v, a, c, b) : CdtSweeper.InCircle(v, a, b, c);

        void TraceEdgeThroughTriangles() {
            EdgeTracer edgeTracer = new EdgeTracer(edge, triangles, front, leftPolygon, rightPolygon);
            edgeTracer.Run();

        }
    }
}