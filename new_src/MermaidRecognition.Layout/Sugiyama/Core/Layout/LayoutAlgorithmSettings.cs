using System;
using System.ComponentModel;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RectanglePacking;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
#if TEST_MSAGL
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout;
using MermaidRecognition.Layout.Sugiyama.Routing;
#endif

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout {

#if !SILVERLIGHT
    ///<summary>
    /// controls many properties of the layout algorithm
    ///</summary>
    [Description("Specifies the layout algorithm parameters")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    [DisplayName("Layout algorithm settings")]
#endif
#if TEST_MSAGL
    
#endif
    public abstract class LayoutAlgorithmSettings {
        EdgeRoutingSettings edgeRoutingSettings = new EdgeRoutingSettings();
        ///<summary>
        /// defines edge routing behaviour
        ///</summary>
        public EdgeRoutingSettings EdgeRoutingSettings { get => edgeRoutingSettings; set => edgeRoutingSettings = value;
        }
        #region
#if REPORTING
        bool reporting;

        /// <summary>
        /// Controls the reporting facility.
        /// </summary>
        [Description("Controls the reporting facility.")]
        [DefaultValue(false)]
        public bool Reporting { get => reporting; set => reporting = value;
        }
#endif
        #endregion
        #region test_msagl

#if TEST_MSAGL
        static Show show;

        /// <summary>
        /// Used for debugging purposes to display all kind of intermediate curves.
        /// </summary>
        public static Show Show { get => show; set => show = value;
        }

        /// <summary>
        /// Used for debugging purposes to display all kind of intermediate curves with widht and color
        /// </summary>
        public static ShowDebugCurves ShowDebugCurves { get; set; }


        /// <summary>
        /// Used for debugging purposes to display all kind of intermediate curves with width and color
        /// </summary>
        public static ShowDebugCurvesEnumeration ShowDebugCurvesEnumeration { get; set; }


        static ShowDatabase showDataBase;

        /// <summary>
        /// used for debugging purposes to display anchors and paths that are kept in the DataBase
        /// </summary>
        public static ShowDatabase ShowDatabase { get => showDataBase; set => showDataBase = value;
        }

        ///<summary>
        ///</summary>
        public static ShowGraph ShowGraph { get; set; }

#endif

        #endregion

        double packingAspectRatio = PackingConstants.GoldenRatio;

        /// <summary>
        /// Controls the ideal aspect ratio for packing disconnected components
        /// </summary>
        public double PackingAspectRatio { get => packingAspectRatio; set => packingAspectRatio = value;
        }

        PackingMethod packingMethod = PackingMethod.Compact;

        /// <summary>
        /// the packing method
        /// </summary>
        public PackingMethod PackingMethod { get => packingMethod; set => packingMethod = value;
        }

        double nodeSeparation = 10;

        /// <summary>
        /// When AvoidOverlaps is set, we optionally enforce a little extra space around nodes
        /// </summary>
        public double NodeSeparation { get => nodeSeparation; set => nodeSeparation = value;
        }

        double clusterMargin = 10;
        
        
        /// <summary>
        /// When AvoidOverlaps is set, we optionally enforce a little extra space between nodes and cluster boundaries
        /// </summary>
        public double ClusterMargin { get => clusterMargin; set => clusterMargin = value;
        }


        /// <summary>
        /// Clones the object
        /// </summary>
        /// <returns></returns>
        public abstract LayoutAlgorithmSettings Clone();
    }
}