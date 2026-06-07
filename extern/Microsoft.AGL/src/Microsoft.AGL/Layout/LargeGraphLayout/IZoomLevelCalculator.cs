using System.Collections.Generic;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.LargeGraphLayout {
    internal interface IZoomLevelCalculator {
        /// <summary>
        /// returns the top node
        /// </summary>
        /// <returns></returns>
        void Run();

        /// <summary>
        /// the graph under layout
        /// </summary>
        GeometryGraph Graph { get; set; }

        /// <summary>
        /// layout settings
        /// </summary>
        LgLayoutSettings Settings { get; set; }

        List<LgNodeInfo> SortedLgNodeInfos { get; }

        List<int> LevelNodeCounts { get; }

    }
}