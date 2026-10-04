using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.DataStructures.RBTree;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation {
    internal class EdgeInserter {
        readonly CdtEdge edge;
        readonly Set<CdtTriangle> triangles;
        readonly RbTree<CdtFrontElement> front;
        readonly Func<CdtSite, CdtSite, CdtEdge> createEdgeDelegate;
        List<CdtSite> rightPolygon = [];
        List<CdtSite> leftPolygon = [];
        List<CdtTriangle> addedTriangles=[];

        public EdgeInserter(CdtEdge edge, Set<CdtTriangle> triangles, RbTree<CdtFrontElement> front, Func<CdtSite, CdtSite, CdtEdge> createEdgeDelegate) {
            this.edge = edge;
            this.triangles = triangles;
            this.front = front;
            this.createEdgeDelegate = createEdgeDelegate;
        }

        public void Run() {
            TraceEdgeThroughTriangles();
            TriangulatePolygon(rightPolygon, edge.upperSite, edge.lowerSite, true);
            TriangulatePolygon(leftPolygon, edge.upperSite, edge.lowerSite, false);
            UpdateFront();
        }

        void UpdateFront() {
            Set<CdtEdge> newFrontEdges = new Set<CdtEdge>();
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