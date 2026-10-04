using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation
{
    ///<summary>
    ///</summary>
    ///<remarks>
    ///</remarks>
    ///<param name="isolatedSite"></param>
    [DebuggerDisplay("{Point}")]
#if TEST_MSAGL
    
#endif
    public class CdtSite(Point isolatedSite)
    {
        /// <summary>
        /// Object to which this site refers to.
        /// </summary>
        public object Owner { get; set; }

        internal IEnumerable<CdtEdge> AllEdges
        {
            get
            {
                if (Edges != null)
                {
                    foreach (CdtEdge e in Edges)
                    {
                        yield return e;
                    }
                }

                if (InEdges != null)
                {
                    foreach (CdtEdge e in InEdges)
                    {
                        yield return e;
                    }
                }
            }
        }

        ///<summary>
        ///</summary>
        public Point Point = isolatedSite;
        /// <summary>
        /// each CdtSite points to the edges for which it is the upper virtex ( for horizontal edges it is the left point)
        /// </summary>
        public List<CdtEdge> Edges;

        internal List<CdtEdge> InEdges;

        internal void AddEdgeToSite(CdtEdge edge)
        {
            if (Edges == null)
            {
                Edges = [];
            }

            Edges.Add(edge);
        }
#if DEBUG && TEST_MSAGL
        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString() => Point.ToString();
#endif

        internal CdtEdge EdgeBetweenUpperSiteAndLowerSite(CdtSite b)
        {
            Debug.Assert(Cdt.Above(this, b) > 0);
            if (Edges != null)
            {
                foreach (CdtEdge edge in Edges)
                {
                    if (edge.lowerSite == b)
                    {
                        return edge;
                    }
                }
            }

            return null;
        }

        internal void AddInEdge(CdtEdge e)
        {
            if (InEdges == null)
            {
                InEdges = [];
            }

            InEdges.Add(e);
        }
        /// <summary>
        /// enumerates over all site triangles
        /// </summary>
        internal IEnumerable<CdtTriangle> Triangles
        {
            // this function might not work correctly if InEdges are not set
            get
            {
                CdtEdge edge;
                if (Edges != null && Edges.Count>0)
                {
                    edge = Edges[0];
                }
                else if (InEdges != null && InEdges.Count>0)
                {
                    edge = InEdges[0];
                }
                else
                {
                    yield break;
                }

                //going counterclockwise around the site
                CdtEdge? e = edge;
                do
                {
                    CdtTriangle? t = e.upperSite == this ? e.CcwTriangle : e.CwTriangle;
                    if (t == null)
                    {
                        e = null;
                        break;
                    }
                    yield return t;
                    e = t.Edges[t.Edges.Index(e) + 2];
                } while (e != edge);//full circle

                if (e != edge)
                { //we have not done the full circle, starting again with edge but now going clockwise around the site
                    e = edge;
                    do
                    {
                        CdtTriangle? t = e.upperSite == this ? e.CwTriangle : e.CcwTriangle;
                        if (t == null)
                        {
                            break;
                        }
                        yield return t;
                        e = t.Edges[t.Edges.Index(e) + 1];
                    } while (true); // we will hit a null triangle for the convex hull border edge
                }
            }
            
        }
    }
}
