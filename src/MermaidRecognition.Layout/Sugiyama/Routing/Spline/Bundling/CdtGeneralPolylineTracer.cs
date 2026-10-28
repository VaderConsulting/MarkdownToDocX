using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    internal class CdtGeneralPolylineTracer {
        RectangleNode<CdtSite> SiteHierarchy { get; set; }
        readonly IEnumerator<PolylinePoint> iterator;
        PolylinePoint segStart;
        readonly Stack<PolylinePoint> polylineHead=new Stack<PolylinePoint>();//we push each processed PolylinePoint to the stack
        PolylinePoint PrevSegStart => polylineHead.Count == 0 ? null : polylineHead.Peek();
        readonly Set<CdtEdge> crossedEdges = [];
        object lastCrossedFeature;

        PolylinePoint segEnd;
        const int OutsideOfTriangulation = 0;
        RectangleNode<CdtTriangle> TriangleHierarchy { get; set; }

        internal CdtGeneralPolylineTracer(IEnumerable<PolylinePoint> polylinePoints, RectangleNode<CdtSite> siteHierarchy, RectangleNode<CdtTriangle> triangleHierarchy) {
            iterator = polylinePoints.GetEnumerator();
            TriangleHierarchy = triangleHierarchy;
            SiteHierarchy = siteHierarchy;
        }



        void TryInsertEdge(CdtEdge e) {
            if(crossedEdges.Contains(e))
            {
                return;
            }

            if (e.upperSite.Owner != e.lowerSite.Owner && RealCrossing(e))
            {
                crossedEdges.Insert(e);
            }
        }

        bool RealCrossing(CdtEdge cdtEdge) {
            TriangleOrientation segEndOrientation = Point.GetTriangleOrientation(segEnd.Point, cdtEdge.upperSite.Point, cdtEdge.lowerSite.Point);
            TriangleOrientation segStartOrientation = Point.GetTriangleOrientation(segStart.Point, cdtEdge.upperSite.Point,
                                                          cdtEdge.lowerSite.Point);

            return (segStartOrientation != TriangleOrientation.Collinear && segStartOrientation != segEndOrientation) ||
                    SomePreviousPointIsOnOtherSiteOfEdge(cdtEdge, segEndOrientation);

        }

        bool SomePreviousPointIsOnOtherSiteOfEdge(CdtEdge cdtEdge, TriangleOrientation segEndOrientation) {
            foreach (PolylinePoint polylinePoint in polylineHead) {
                TriangleOrientation orientation = Point.GetTriangleOrientation(polylinePoint.Point, cdtEdge.upperSite.Point,
                                                               cdtEdge.lowerSite.Point);
                if (orientation != TriangleOrientation.Collinear)
                {
                    return orientation != segEndOrientation;
                }
            }
            return false;
        }


      

        static bool PointBelongsToInteriorOfTriangle(Point point, CdtTriangle cdtTriangle) {
            for (int i = 0; i < 3; i++)
            {
                if (Point.GetTriangleOrientation(cdtTriangle.Sites[i].Point, cdtTriangle.Sites[i + 1].Point,
                                                 point) != TriangleOrientation.Counterclockwise)
                {
                    return false;
                }
            }

            return true;
        }


        object GetPointFeatureOnHierarchies(PolylinePoint pp) {
            RectangleNode<CdtSite> hitNode = SiteHierarchy.FirstHitNode(pp.Point);
            return hitNode != null
                       ? hitNode.UserData
                       : GetFirstTriangleFeatureNotSite(pp);

        }

        

        object GetFirstTriangleFeatureNotSite(PolylinePoint pp) {
            foreach (CdtTriangle triangle in TriangleHierarchy.AllHitItems(pp.Point)) {
                object feature = TryCreateFeatureWhichIsNotSite(pp, triangle);
                if (feature != null)
                {
                    return feature;
                }
            }
            return OutsideOfTriangulation;
        }

        static object TryCreateFeatureWhichIsNotSite(PolylinePoint pp, CdtTriangle triangle) {
            Debug.Assert(!triangle.Sites.Any(s => ApproximateComparer.Close(pp.Point, s.Point)));
            TriangleOrientation a0 = Point.GetTriangleOrientation(pp.Point, triangle.Sites[0].Point, triangle.Sites[1].Point);
            if (a0 == TriangleOrientation.Clockwise)
            {
                return null;
            }

            TriangleOrientation a1 = Point.GetTriangleOrientation(pp.Point, triangle.Sites[1].Point, triangle.Sites[2].Point);
            if (a1 == TriangleOrientation.Clockwise)
            {
                return null;
            }

            TriangleOrientation a2 = Point.GetTriangleOrientation(pp.Point, triangle.Sites[2].Point, triangle.Sites[3].Point);
            if (a2 == TriangleOrientation.Clockwise)
            {
                return null;
            }

            if (a0 == TriangleOrientation.Counterclockwise &&
                a1 == TriangleOrientation.Counterclockwise &&
                a2 == TriangleOrientation.Counterclockwise)
            {
                return triangle;
            }

            if (a0 == TriangleOrientation.Collinear)
            {
                return triangle.Edges[0];
            }

            if (a1 == TriangleOrientation.Collinear)
            {
                return triangle.Edges[1];
            }

            Debug.Assert(a2 == TriangleOrientation.Collinear);
            return triangle.Edges[2];
        }

        internal Set<CdtEdge> GetCrossedEdges() {
           
            if (iterator.MoveNext() == false)
            {
                return crossedEdges;
            }

            segStart = iterator.Current;
            lastCrossedFeature = GetPointFeatureOnHierarchies(segStart);
            if (iterator.MoveNext() == false)
            {
                return crossedEdges;
            }

            segEnd = iterator.Current;
            do {
                Step();
                if (LastCrossedFeatureContainsSegEnd()) {
                    if (iterator.MoveNext() == false)
                    {
                        return crossedEdges;
                    }

                    MoveSegmentForward();
                }
            } while (true);
        }

        void MoveSegmentForward() {
            polylineHead.Push(segStart);
            segStart = segEnd;
            segEnd = iterator.Current;
        }

        bool LastCrossedFeatureContainsSegEnd() {
            if (lastCrossedFeature is CdtTriangle triangle)
            {
                return PointBelongsToInteriorOfTriangle(segEnd.Point, triangle);
            }

            if (lastCrossedFeature is CdtEdge edge)
            {
                return Point.GetTriangleOrientation(edge.upperSite.Point, edge.lowerSite.Point, segEnd.Point) ==
                       TriangleOrientation.Collinear;
            }

            if (lastCrossedFeature is CdtSite site)
            {
                return ApproximateComparer.Close(site.Point, segEnd.Point);
            }

            return true; //segEnd belongs to the outside of the triangulation
        }

        void Step() {
            if (PrevSegStart == null)
            {
                ProcessFirstSegment();
            }
            else
            {
                RegularStep();
            }
        }


        /*
        void DrawLastFeature() {
            var l = new List<DebugCurve>();

            l.AddRange(
                cdt.GetTriangles().Select(
                    t => new DebugCurve(100, 1, "green", new Polyline(t.Sites.Select(v => v.Point)) {Closed = true})));
            
            var triangle = lastCrossedFeature as CdtTriangle;
            ICurve lfc;
            if (triangle != null)
                lfc = new Polyline(triangle.Sites.Select(s => s.Point)) {Closed = true};
            else {
                var edge = lastCrossedFeature as CdtEdge;
                if (edge != null)
                    lfc = new LineSegment(edge.upperSite.Point, edge.lowerSite.Point);
                else
                    lfc = new Ellipse(10, 10, ((CdtSite) lastCrossedFeature).Point);
            }
            l.Add(new DebugCurve(100, 3, "brown", lfc));
            if(PrevSegStart!=null)
                l.Add(new DebugCurve(100,1,"red", new LineSegment(PrevSegStart.Point,segStart.Point)));
            l.Add(new DebugCurve(100, 1, "red", new LineSegment(segStart.Point, segEnd.Point)));

            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(l);

        }
        
        */

        void RegularStep() {
            if (lastCrossedFeature is CdtTriangle triangle)
            {
                StepFromTriangleInteriorPoint(triangle);
            }
            else
            {
                if (lastCrossedFeature is CdtEdge edge)
                {
                    StepFromEdgeInteriorPoint(edge);
                }
                else
                {
                    if (lastCrossedFeature is CdtSite site)
                    {
                        RegularStepFromSite(site);
                    }
                    else
                    { //we are outside of the triangulation  
                        //we don't report crossed edges that are on the triangulation boundary here because they are free to cross
                        lastCrossedFeature = GetPointFeatureOnHierarchies(segEnd);
                    }
                }
            }
        }

      
        void RegularStepFromSite(CdtSite site) {
            CdtTriangle triangle = null;
            foreach (CdtTriangle t in site.Triangles) {
                int index = t.Sites.Index(site);
                if (PointIsInsideCone(segEnd.Point, site.Point, t.Sites[index + 2].Point, t.Sites[index + 1].Point)) {
                    triangle = t;
                    TriangleOrientation segEndOrientation = Point.GetTriangleOrientation(segEnd.Point, t.Sites[index + 1].Point,
                                                                                         t.Sites[index + 2].Point);
                    if (segEndOrientation != TriangleOrientation.Counterclockwise) {
                        CrossTriangleEdge(t.OppositeEdge(site));
                        return;
                    }
                }
            }
            if (triangle == null) {
                lastCrossedFeature = OutsideOfTriangulation;
                return;
            }
            if (PointBelongsToInteriorOfTriangle(segEnd.Point, triangle))
            {
                lastCrossedFeature = triangle;
            }
            else
            {
                foreach (CdtEdge? e in triangle.Edges.Where(e=>e.IsAdjacent(site))) {
                    if (Point.GetTriangleOrientation( e.upperSite.Point, e.lowerSite.Point, segEnd.Point)==TriangleOrientation.Collinear) {
                        lastCrossedFeature = e;
                        return;
                    }
                }
            }
        }
        
        
        void ProcessFirstSegment() {
            if (lastCrossedFeature is CdtTriangle triangle)
            {
                StepFromTriangleInteriorPoint(triangle);
            }
            else
            {
                if (lastCrossedFeature is CdtEdge edge)
                {
                    StepFromEdgeInteriorPoint(edge);
                }
                else
                {
                    Debug.Assert(lastCrossedFeature is CdtSite);
                    RegularStepFromSite(lastCrossedFeature as CdtSite);
                }
            }
        }

        static CdtTriangle GetTriangleOnThePointSide(CdtEdge edge, Point p) => Point.GetTriangleOrientation(edge.upperSite.Point, edge.lowerSite.Point, p) ==
                 TriangleOrientation.Counterclockwise
                     ? edge.CcwTriangle
                     : edge.CwTriangle;

        void StepFromEdgeInteriorPoint(CdtEdge edge) {
            CdtTriangle triangle = GetTriangleOnThePointSide(edge, segEnd.Point);
            if (triangle == null) {
                lastCrossedFeature = OutsideOfTriangulation;
                return;
            }
            if (PointBelongsToInteriorOfTriangle(segEnd.Point, triangle)) {
                lastCrossedFeature = triangle;
                return;
            }

            //we cross an edge
            for (int i = 0; i < 3; i++) {
                if (triangle.Edges[i] == edge)
                {
                    continue;
                }

                if (PointIsInsideCone(segEnd.Point, segStart.Point, triangle.Sites[i + 1].Point,
                                            triangle.Sites[i].Point)) {
                    CrossTriangleEdge(triangle.Edges[i]);
                    return;
                }
            }

        }

        static bool PointIsInsideCone(Point p, Point apex, Point leftSideP, Point rightSideP) => Point.GetTriangleOrientation(apex, leftSideP, p) != TriangleOrientation.Counterclockwise &&
                Point.GetTriangleOrientation(apex, rightSideP, p) != TriangleOrientation.Clockwise;

        void StepFromTriangleInteriorPoint(CdtTriangle triangle) {
            if (PointBelongsToInteriorOfTriangle(segEnd.Point, triangle)) {
                lastCrossedFeature = triangle;
                return;
            }
            //we cross an edge
            for (int i = 0; i < 3; i++)
            {
                if (PointIsInsideCone(segEnd.Point, segStart.Point, triangle.Sites[i + 1].Point,
                                            triangle.Sites[i].Point)) {
                    CrossTriangleEdge(triangle.Edges[i]);
                    return;
                }
            }
        }

        void CrossTriangleEdge(CdtEdge e) {
            TryInsertEdge(e);
            if (Point.GetTriangleOrientation(segStart.Point, e.upperSite.Point, segEnd.Point) ==
                TriangleOrientation.Collinear) {
                lastCrossedFeature = e.upperSite;
            //    PickupEdgesOfSite(e.upperSite);
            } else if (Point.GetTriangleOrientation(segStart.Point, e.lowerSite.Point, segEnd.Point) ==
                       TriangleOrientation.Collinear) {
                lastCrossedFeature = e.lowerSite;
              //  PickupEdgesOfSite(e.lowerSite);
            } else
            {
                lastCrossedFeature = e;
            }
        }

/*
        static void PickupEdgesOfSite(CdtSite site) {
            //throw new NotImplementedException();
        }
*/
    }

}
