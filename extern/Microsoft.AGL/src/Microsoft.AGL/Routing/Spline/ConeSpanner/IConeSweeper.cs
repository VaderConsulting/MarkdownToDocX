using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Spline.ConeSpanner {
    internal interface IConeSweeper {
        Point ConeRightSideDirection { get; set; }
        Point ConeLeftSideDirection { get; set; }
        Point SweepDirection { get; set; }
        double Z { get; set; }
    }
}