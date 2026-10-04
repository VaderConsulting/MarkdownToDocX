using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal interface IConeSweeper {
        Point ConeRightSideDirection { get; set; }
        Point ConeLeftSideDirection { get; set; }
        Point SweepDirection { get; set; }
        double Z { get; set; }
    }
}