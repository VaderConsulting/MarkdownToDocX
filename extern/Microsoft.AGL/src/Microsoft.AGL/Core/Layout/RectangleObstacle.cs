using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Core.Layout
{
    internal class RectangleObstacle : IObstacle
    {
        internal RectangleObstacle(Rectangle r) => Rectangle = r;

        internal RectangleObstacle(Rectangle r, object data)
        {
            Rectangle = r;
            Data = data;
        }

        public Rectangle Rectangle
        {
            get;
            private set;
        }

        public object Data
        {
            get;
            private set;
        }
    }
}