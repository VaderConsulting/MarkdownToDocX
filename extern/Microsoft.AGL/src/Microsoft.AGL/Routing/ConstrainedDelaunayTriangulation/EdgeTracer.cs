using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.DataStructures.RBTree;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation {
    internal class EdgeTracer(CdtEdge edge, Set<CdtTriangle> triangles, RbTree<CdtFrontElement> front, List<CdtSite> leftPolygon, List<CdtSite> rightPolygon)
    {
        readonly CdtEdge edge = edge;
        readonly Set<CdtTriangle> triangles = triangles;
        readonly RbTree<CdtFrontElement> front = front;
        readonly List<CdtSite> leftPolygon = leftPolygon;
        readonly List<CdtSite> rightPolygon = rightPolygon;

        /// <summary>
        /// the upper site of the edge
        /// </summary>
        CdtSite a = edge.upperSite;
        /// <summary>
        /// the lower site of the edge
        /// </summary>
        CdtSite b = edge.lowerSite;
        CdtEdge piercedEdge;
        CdtTriangle piercedTriangle;
        RBNode<CdtFrontElement> piercedToTheLeftFrontElemNode;
        RBNode<CdtFrontElement> piercedToTheRightFrontElemNode;
        List<CdtFrontElement> elementsToBeRemovedFromFront = [];
        List<CdtTriangle> removedTriangles = [];

        public void Run() {
            Init();
            Traverse();
        }

        void Traverse() {
            while (!BIsReached()) {
                if (piercedToTheLeftFrontElemNode != null) { ProcessLeftFrontPiercedElement(); }
                else if (piercedToTheRightFrontElemNode != null) {
                    ProcessRightFrontPiercedElement();
                }
                else
                {
                    ProcessPiercedEdge();
                }
            }
            if (piercedTriangle != null)
            {
                RemovePiercedTriangle(piercedTriangle);
            }

            FindMoreRemovedFromFrontElements();

            foreach (CdtFrontElement elem in elementsToBeRemovedFromFront)
            {
                front.Remove(elem);
            }
        }

        void ProcessLeftFrontPiercedElement() {
            // CdtSweeper.ShowFront(triangles, front,new []{new LineSegment(a.Point, b.Point),new LineSegment(piercedToTheLeftFrontElemNode.Item.Edge.lowerSite.Point,piercedToTheLeftFrontElemNode.Item.Edge.upperSite.Point)},null);
            RBNode<CdtFrontElement> v = piercedToTheLeftFrontElemNode;

            do {
                elementsToBeRemovedFromFront.Add(v.Item);
                AddSiteToLeftPolygon(v.Item.LeftSite);
                v = front.Previous(v);
            } while (Point.PointToTheLeftOfLine(v.Item.LeftSite.Point, a.Point, b.Point)); //that is why we are adding to the left polygon
            elementsToBeRemovedFromFront.Add(v.Item);
            AddSiteToRightPolygon(v.Item.LeftSite);
            if (v.Item.LeftSite == b) {
                piercedToTheLeftFrontElemNode = v; //this will stop the traversal
                return;
            }
            FindPiercedTriangle(v);
            piercedToTheLeftFrontElemNode = null;
        }

        void FindPiercedTriangle(RBNode<CdtFrontElement> v) {
            CdtEdge e = v.Item.Edge;
            CdtTriangle t = e.CcwTriangle ?? e.CwTriangle;
            int eIndex = t.Edges.Index(e);
            for (int i = 1; i <= 2; i++) {
                CdtEdge ei = t.Edges[i + eIndex];
                int signedArea0 = ApproximateComparer.Sign(Point.SignedDoubledTriangleArea(ei.lowerSite.Point, a.Point, b.Point));
                int signedArea1 = ApproximateComparer.Sign(Point.SignedDoubledTriangleArea(ei.upperSite.Point, a.Point, b.Point));
                if (signedArea1 * signedArea0 <= 0) {
                    piercedTriangle = t;
                    piercedEdge = ei;
                    break;
                }
            }
        }

        void FindMoreRemovedFromFrontElements() {
            foreach (CdtTriangle triangle in removedTriangles) {
                foreach (CdtEdge e in triangle.Edges) {
                    if (e.CcwTriangle == null && e.CwTriangle == null) {
                        CdtSite site = e.upperSite.Point.X < e.lowerSite.Point.X ? e.upperSite : e.lowerSite;
                        RBNode<CdtFrontElement> frontNode = CdtSweeper.FindNodeInFrontBySite(front, site);
                        if (frontNode.Item.Edge == e)
                        {
                            elementsToBeRemovedFromFront.Add(frontNode.Item);
                        }
                    }
                }
            }
        }

        void ProcessPiercedEdge() {
            //if(CdtSweeper.db)
              //          CdtSweeper.ShowFront(triangles, front, new[] { new LineSegment(a.Point, b.Point) },
                //                      new[] { new LineSegment(piercedEdge.upperSite.Point, piercedEdge.lowerSite.Point) });
            if (piercedEdge.CcwTriangle == piercedTriangle) {
                AddSiteToLeftPolygon(piercedEdge.lowerSite);
                AddSiteToRightPolygon(piercedEdge.upperSite);
            }
            else {
                AddSiteToLeftPolygon(piercedEdge.upperSite);
                AddSiteToRightPolygon(piercedEdge.lowerSite);
            }

            RemovePiercedTriangle(piercedTriangle);
            PrepareNextStateAfterPiercedEdge();
        }

        void PrepareNextStateAfterPiercedEdge() {
            CdtTriangle t = piercedEdge.CwTriangle ?? piercedEdge.CcwTriangle;
            int eIndex = t.Edges.Index(piercedEdge);
            for (int i = 1; i <= 2; i++) {
                CdtEdge e = t.Edges[i + eIndex];
                int signedArea0 = ApproximateComparer.Sign(Point.SignedDoubledTriangleArea(e.lowerSite.Point, a.Point, b.Point));
                int signedArea1 = ApproximateComparer.Sign(Point.SignedDoubledTriangleArea(e.upperSite.Point, a.Point, b.Point));
                if (signedArea1 * signedArea0 <= 0) {
                    if (e.CwTriangle != null && e.CcwTriangle != null) {
                        piercedTriangle = t;
                        piercedEdge = e;
                        break;
                    }
                    //e has to belong to the front, and its triangle has to be removed
                    piercedTriangle = null;
                    piercedEdge = null;
                    CdtSite leftSite = e.upperSite.Point.X < e.lowerSite.Point.X ? e.upperSite : e.lowerSite;
                    RBNode<CdtFrontElement> frontElem = CdtSweeper.FindNodeInFrontBySite(front, leftSite);
                    Debug.Assert(frontElem != null);
                    if (leftSite.Point.X < a.Point.X)
                    {
                        piercedToTheLeftFrontElemNode = frontElem;
                    }
                    else
                    {
                        piercedToTheRightFrontElemNode = frontElem;
                    }

                    RemovePiercedTriangle(e.CwTriangle ?? e.CcwTriangle);
                    break;
                }
            }
        }

        void RemovePiercedTriangle(CdtTriangle t) {
            triangles.Remove(t);
            foreach (CdtEdge e in t.Edges)
            {
                if (e.CwTriangle == t)
                {
                    e.CwTriangle = null;
                }
                else
                {
                    e.CcwTriangle = null;
                }
            }

            removedTriangles.Add(t);
        }


        void ProcessRightFrontPiercedElement() {
            RBNode<CdtFrontElement> v = piercedToTheRightFrontElemNode;

            do {
                elementsToBeRemovedFromFront.Add(v.Item);
                AddSiteToRightPolygon(v.Item.RightSite);
                v = front.Next(v);
            } while (Point.PointToTheRightOfLine(v.Item.RightSite.Point, a.Point, b.Point)); //that is why we are adding to the right polygon
            elementsToBeRemovedFromFront.Add(v.Item);
            AddSiteToLeftPolygon(v.Item.RightSite);
            if (v.Item.RightSite == b) {
                piercedToTheRightFrontElemNode = v; //this will stop the traversal
                return;
            }
            FindPiercedTriangle(v);
            piercedToTheRightFrontElemNode = null;
        }

        void AddSiteToLeftPolygon(CdtSite site) => AddSiteToPolygonWithCheck(site, leftPolygon);

        void AddSiteToPolygonWithCheck(CdtSite site, List<CdtSite> list) {
            if (site == b)
            {
                return;
            }

            if (list.Count == 0 || list[list.Count - 1] != site)
            {
                list.Add(site);
            }
        }

        void AddSiteToRightPolygon(CdtSite site) => AddSiteToPolygonWithCheck(site, rightPolygon);

        bool BIsReached() {
            RBNode<CdtFrontElement> node = piercedToTheLeftFrontElemNode ?? piercedToTheRightFrontElemNode;
            if (node != null)
            {
                return node.Item.Edge.IsAdjacent(b);
            }

            return piercedEdge.IsAdjacent(b);
        }

        void Init() {
            //            if (CdtSweeper.D)
            //                CdtSweeper.ShowFront(triangles, front, new[] {new LineSegment(a.Point, b.Point)},null);
            //new[] {new LineSegment(piercedEdge.upperSite.Point, piercedEdge.lowerSite.Point)});
            RBNode<CdtFrontElement> frontElemNodeRightOfA = CdtSweeper.FindNodeInFrontBySite(front, a);
            RBNode<CdtFrontElement> frontElemNodeLeftOfA = front.Previous(frontElemNodeRightOfA);
            if (Point.PointToTheLeftOfLine(b.Point, frontElemNodeLeftOfA.Item.LeftSite.Point, frontElemNodeLeftOfA.Item.RightSite.Point))
            {
                piercedToTheLeftFrontElemNode = frontElemNodeLeftOfA;
            }
            else if (Point.PointToTheRightOfLine(b.Point, frontElemNodeRightOfA.Item.RightSite.Point, frontElemNodeRightOfA.Item.LeftSite.Point))
            {
                piercedToTheRightFrontElemNode = frontElemNodeRightOfA;
            }
            else {
                foreach (CdtEdge e in a.Edges) {
                    CdtTriangle t = e.CcwTriangle;
                    if (t == null)
                    {
                        continue;
                    }

                    if (Point.PointToTheLeftOfLine(b.Point, e.lowerSite.Point, e.upperSite.Point))
                    {
                        continue;
                    }

                    int eIndex = t.Edges.Index(e);
                    CdtSite site = t.Sites[eIndex + 2];
                    if (Point.PointToTheLeftOfLineOrOnLine(b.Point, site.Point, e.upperSite.Point)) {
                        piercedEdge = t.Edges[eIndex + 1];
                        piercedTriangle = t;
//                                                CdtSweeper.ShowFront(triangles, front, new[] { new LineSegment(e.upperSite.Point, e.lowerSite.Point) }, 
//                                                    new[] { new LineSegment(piercedEdge.upperSite.Point, piercedEdge.lowerSite.Point) });
                        break;
                    }
                }
            }
        }


    }
}