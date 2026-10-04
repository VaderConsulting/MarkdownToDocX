namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry {
    internal class HullPoint {
        Point point;

        internal Point Point { get => point; set => point = value;
        }
        bool deleted;

        internal bool Deleted { get => deleted; set => deleted = value;
        }

        internal HullPoint(Point point) => Point = point;

        public override string ToString() => point + (Deleted ? "X" : "");
    }
}
