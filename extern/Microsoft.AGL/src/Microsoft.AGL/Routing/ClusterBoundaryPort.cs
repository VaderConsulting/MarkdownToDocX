using System;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Routing {
    ///<summary>
    ///this is a port for routing from a cluster
    ///</summary>
    public class ClusterBoundaryPort : RelativeFloatingPort {
        Polyline loosePolyline;
        internal Polyline LoosePolyline { get => loosePolyline; set => loosePolyline = value;
        }

        ///<summary>
        ///constructor
        ///</summary>
        ///<param name="curveDelegate"></param>
        ///<param name="centerDelegate"></param>
        ///<param name="locationOffset"></param>
        public ClusterBoundaryPort(Func<ICurve> curveDelegate, Func<Point> centerDelegate, Point locationOffset)
            : base(curveDelegate, centerDelegate, locationOffset) { }

        ///<summary>
        ///constructor 
        ///</summary>
        ///<param name="curveDelegate"></param>
        ///<param name="centerDelegate"></param>
        public ClusterBoundaryPort(Func<ICurve> curveDelegate, Func<Point> centerDelegate)
            : base(curveDelegate, centerDelegate) { }
    }
}