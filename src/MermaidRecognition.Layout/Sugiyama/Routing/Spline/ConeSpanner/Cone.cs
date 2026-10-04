using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal class Cone {
        bool removed;

        internal bool Removed { get => removed; set => removed = value;
        }
        Point apex;
        readonly IConeSweeper coneSweeper;

        internal Cone(Point apex, IConeSweeper coneSweeper) {
            this.apex = apex;
            this.coneSweeper = coneSweeper;
        }

        internal Point Apex { get => apex; set => apex = value;
        }

        internal Point RightSideDirection => coneSweeper.ConeRightSideDirection;

        internal Point LeftSideDirection => coneSweeper.ConeLeftSideDirection;



        private ConeSide rightSide;

        internal ConeSide RightSide
        {
            get => rightSide;
            set
            {
                rightSide = value;
                rightSide.Cone = this;
            }
        }
        private ConeSide leftSide;

        internal ConeSide LeftSide
        {
            get => leftSide;
            set
            {
                leftSide = value;
                leftSide.Cone = this;
            }
        }

        internal bool PointIsInside(Point p) => Point.PointToTheRightOfLineOrOnLine(p, Apex, Apex + LeftSideDirection) &&
            Point.PointToTheLeftOfLineOrOnLine(p, Apex, Apex + RightSideDirection);
    }
}
