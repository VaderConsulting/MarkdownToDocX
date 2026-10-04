namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry
{
    internal class HullStack {
        Point hullPoint;

        internal Point Point { get => hullPoint; set => hullPoint = value;
        }
        HullStack next;

        internal HullStack Next { get => next; set => next = value;
        }

        internal HullStack(Point hullPoint) => Point = hullPoint;

        public override string ToString() => hullPoint.ToString();

    }
}
