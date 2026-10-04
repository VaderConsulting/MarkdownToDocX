using System;

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout {
    /// <summary>
    /// the base class for LgNodeInfo and LgEdgeInfo
    /// </summary>
    public class LgInfoBase {
        double _slidingZoomLevel = double.PositiveInfinity;
        double _zoomLevel = double.PositiveInfinity;
        double _rank;

        /// <summary>
        /// 
        /// </summary>
        public double SlidingZoomLevel { get => _slidingZoomLevel; set => _slidingZoomLevel = value;
        }

        /// <summary>
        /// if the zoom is at least ZoomLevel the node should be rendered
        /// </summary>
        public double ZoomLevel { get => Math.Min(_zoomLevel, SlidingZoomLevel); set => _zoomLevel = value;
        }

        /// <summary>
        /// the rank of the element
        /// </summary>
        public double Rank { get => _rank; set => _rank = value;
        }

        internal bool ZoomLevelIsNotSet => ZoomLevel == double.PositiveInfinity;
    }
}