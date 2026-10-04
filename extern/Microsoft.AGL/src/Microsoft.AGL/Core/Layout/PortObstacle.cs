using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Core.Layout{
    struct PortObstacle : IObstacle {
        internal Point Location;
        internal PortObstacle(Point c) => Location = c;
        /// <summary>
        /// 
        /// </summary>
        public Rectangle Rectangle => new Rectangle(Location);
    }
}