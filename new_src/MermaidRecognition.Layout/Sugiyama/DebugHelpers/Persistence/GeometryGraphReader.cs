using MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout;
using MermaidRecognition.Layout.Sugiyama.Miscellaneous.Ranking;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.OverlapRemoval;





#if PERSISTENCE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Layout.Incremental;
using MermaidRecognition.Layout.Sugiyama.Layout.Layered;
using MermaidRecognition.Layout.Sugiyama.Layout.MDS;
using SymmetricSegment = MermaidRecognition.Layout.Sugiyama.Core.DataStructures.SymmetricTuple<MermaidRecognition.Layout.Sugiyama.Core.Geometry.Point>;

namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers.Persistence {

#if SILVERLIGHT
    public class InvalidDataException : Exception
    {
        public InvalidDataException(string s)
            : base(s)
        {
        }
    }
#endif

    /// <summary>
    /// reads the GeometryGraph from a file
    /// </summary>
    public class GeometryGraphReader : IDisposable {
        /// <summary>
        /// the list of edges, needed to match it with GraphReader edges
        /// </summary>
        public IList<Edge> EdgeList = [];

        readonly Dictionary<string, Edge> idToEdges = [];
        readonly Dictionary<string, Rail> idToRails = [];
        readonly Dictionary<string, LgEdgeInfo> railIdsToTopRankedEdgeInfo = [];

        readonly GeometryGraph _graph = new GeometryGraph();

        /// <summary>
        /// The deserialized settings.
        /// </summary>
        public LayoutAlgorithmSettings Settings { get; set; }

        readonly Dictionary<string, ClusterWithChildLists> stringToClusters =
            [];

        readonly Dictionary<string, Node> nodeIdToNodes = [];
#if !SILVERLIGHT
        readonly XmlTextReader xmlTextReader;
#endif

        /// <summary>
        /// an empty constructor
        /// </summary>
        public GeometryGraphReader() {
        }

        /// <summary>
        /// constructor witha given stream
        /// </summary>
        /// <param name="streamP"></param>
        public GeometryGraphReader(Stream streamP) {
            XmlReaderSettings settings = new XmlReaderSettings {IgnoreComments = false, IgnoreWhitespace = true};
#if !SILVERLIGHT
            xmlTextReader = new XmlTextReader(streamP);
            XmlReader = XmlReader.Create(xmlTextReader, settings);
#else
            XmlReader.Create(streamP, settings);
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Cluster FindClusterById(string id) {
            if (stringToClusters.TryGetValue(id, out ClusterWithChildLists? cwl))
            {
                return cwl.Cluster;
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Node FindNodeById(string id) {
            if (nodeIdToNodes.TryGetValue(id, out Node? node))
            {
                return node;
            }

            return null;
        }

        /// <summary>
        /// creates the graph from a given file
        /// </summary>
        /// <returns></returns>
        public static GeometryGraph CreateFromFile(string fileName) => CreateFromFile(fileName, out LayoutAlgorithmSettings? settings);

        /// <summary>
        /// creates the graph and settings from a given file
        /// </summary>
        /// <returns></returns>
        public static GeometryGraph CreateFromFile(string fileName, out LayoutAlgorithmSettings settings) {
#if DEBUG && TEST_MSAGL
            if (FirstCharacter(fileName) != '<') {
                settings = null;
                return null;
            }
#endif
            using (Stream stream = File.OpenRead(fileName)) {
                GeometryGraphReader graphReader = new GeometryGraphReader(stream);
                GeometryGraph graph = graphReader.Read();
                settings = graphReader.Settings;
                return graph;
            }
        }

#if DEBUG && TEST_MSAGL
        static char FirstCharacter(string fileName) {
            using (TextReader reader = File.OpenText(fileName))
            {
                char first = (char)reader.Peek();
                return first;
            }
        }
#endif

        /// <summary>
        /// Reads the graph from the stream
        /// </summary>
        /// <returns></returns>
        public GeometryGraph Read() {
            CultureInfo currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            try {
                ReadGraph();
                return _graph;
            }
            finally {
                Thread.CurrentThread.CurrentCulture = currentCulture;
            }
        }

        /// <summary>
        /// reads the layout algorithm settings
        /// </summary>
        LayoutAlgorithmSettings ReadLayoutAlgorithmSettings(XmlReader reader) {
            LayoutAlgorithmSettings layoutSettings = null;
            CheckToken(GeometryToken.LayoutAlgorithmSettings);
            if (reader.IsEmptyElement) {
                reader.Read();
                return null;
            }
            //reader.Read();

            EdgeRoutingMode edgeRoutingMode =
                (EdgeRoutingMode) GetIntAttributeOrDefault(GeometryToken.EdgeRoutingMode, (int) EdgeRoutingMode.Spline);
            string str = GetAttribute(GeometryToken.LayoutAlgorithmType);
            if (XmlReader.NodeType == XmlNodeType.EndElement) {
                //todo - support fastincremental settings
                layoutSettings = new FastIncrementalLayoutSettings();
                EdgeRoutingSettings routingSettings = layoutSettings.EdgeRoutingSettings;
                routingSettings.EdgeRoutingMode = edgeRoutingMode;
            }
            else {
                if (str != null) {
                    GeometryToken token =
                        (GeometryToken) Enum.Parse(typeof (GeometryToken), str, false);
                    if (token == GeometryToken.SugiyamaLayoutSettings) {
                        layoutSettings = ReadSugiyamaLayoutSettings(edgeRoutingMode);
                    }
                    else if (token == GeometryToken.MdsLayoutSettings) {
                        MdsLayoutSettings mds = new MdsLayoutSettings();
                        EdgeRoutingSettings routingSettings = mds.EdgeRoutingSettings;
                        routingSettings.EdgeRoutingMode = edgeRoutingMode;

                        layoutSettings = mds;
                        if (XmlReader.IsStartElement(GeometryToken.Reporting.ToString())) {
#if REPORTING
                            mds.Reporting =
#endif
                                ReadBooleanElement(GeometryToken.Reporting);
                        }
                        mds.Exponent = ReadDoubleElement(reader);
                        mds.IterationsWithMajorization = ReadIntElement(GeometryToken.IterationsWithMajorization);
                        mds.PivotNumber = ReadIntElement(GeometryToken.PivotNumber);
                        mds.RotationAngle = ReadDoubleElement(reader);
                        mds.ScaleX = ReadDoubleElement(reader);
                        mds.ScaleY = ReadDoubleElement(reader);
                    }
                    else //todo - write a reader and a writer for FastIncrementalLayoutSettings 
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            reader.ReadEndElement();

            return layoutSettings;
        }

        LayoutAlgorithmSettings ReadSugiyamaLayoutSettings(EdgeRoutingMode edgeRoutingMode) {
            SugiyamaLayoutSettings sugiyama = new SugiyamaLayoutSettings();
            EdgeRoutingSettings routingSettings = sugiyama.EdgeRoutingSettings;
            routingSettings.EdgeRoutingMode = edgeRoutingMode;

            LayoutAlgorithmSettings layoutSettings = sugiyama;

            sugiyama.MinNodeWidth = GetDoubleAttributeOrDefault(GeometryToken.MinNodeWidth, sugiyama.MinNodeWidth);
            sugiyama.MinNodeHeight = GetDoubleAttributeOrDefault(GeometryToken.MinNodeHeight, sugiyama.MinimalHeight);
            sugiyama.AspectRatio = GetDoubleAttributeOrDefault(GeometryToken.AspectRatio, sugiyama.AspectRatio);
            sugiyama.NodeSeparation = GetDoubleAttributeOrDefault(GeometryToken.NodeSeparation, sugiyama.NodeSeparation);
            sugiyama.ClusterMargin = sugiyama.NodeSeparation;

#if REPORTING
            sugiyama.Reporting = GetBoolAttributeOrDefault(GeometryToken.Reporting, false);
#endif

            sugiyama.RandomSeedForOrdering = GetIntAttributeOrDefault(GeometryToken.RandomSeedForOrdering,
                sugiyama.RandomSeedForOrdering);
            sugiyama.NoGainAdjacentSwapStepsBound = GetIntAttributeOrDefault(GeometryToken.NoGainStepsBound,
                sugiyama.NoGainAdjacentSwapStepsBound);
            sugiyama.MaxNumberOfPassesInOrdering = GetIntAttributeOrDefault(GeometryToken.MaxNumberOfPassesInOrdering,
                sugiyama.MaxNumberOfPassesInOrdering);
            sugiyama.RepetitionCoefficientForOrdering = GetIntAttributeOrDefault(GeometryToken.
                RepetitionCoefficientForOrdering, sugiyama.RepetitionCoefficientForOrdering);
            sugiyama.GroupSplit = GetIntAttributeOrDefault(GeometryToken.GroupSplit, sugiyama.GroupSplit);
            sugiyama.LabelCornersPreserveCoefficient = GetDoubleAttribute(GeometryToken.LabelCornersPreserveCoefficient);
            sugiyama.BrandesThreshold = GetIntAttributeOrDefault(GeometryToken.BrandesThreshold,
                sugiyama.BrandesThreshold);
            sugiyama.LayerSeparation = GetDoubleAttributeOrDefault(GeometryToken.LayerSeparation,
                sugiyama.LayerSeparation);
            PlaneTransformation transform = new PlaneTransformation();
            ReadTransform(transform);
            return layoutSettings;
        }


        void ReadTransform(PlaneTransformation transform) {
            XmlRead();
            if (TokenIs(GeometryToken.Transform)) {
                XmlRead();
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 3; j++) {
                        CheckToken(GeometryToken.TransformElement);
                        MoveToContent();
                        transform[i, j] = ReadElementContentAsDouble();
                    }
                }

                XmlRead();
            }
            else {
                //set the unit transform
                transform[0, 0] = 1;
                transform[0, 1] = 0;
                transform[0, 2] = 0;
                transform[1, 0] = 0;
                transform[1, 1] = 1;
                transform[1, 2] = 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        bool ReadBooleanElement(GeometryToken tokens) {
            CheckToken(tokens);
            return ReadElementContentAsBoolean();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        bool ReadElementContentAsBoolean() => XmlReader.ReadElementContentAsBoolean();

        int ReadIntElement(GeometryToken token) {
            CheckToken(token);
            return ReadElementContentAsInt();
        }


        static double ReadDoubleElement(XmlReader r) => r.ReadElementContentAsDouble();

        void ReadGraph() {
            MoveToContent();
            _graph.Margins = GetDoubleAttributeOrDefault(GeometryToken.Margins, 10);
            if (XmlReader.Name.ToLower() != GeometryToken.Graph.ToString().ToLower())
            {
                Error("expecting element \"graph\"");
            }

            bool done = false;
            do {
                switch (GetElementTag()) {
                    case GeometryToken.Nodes:
                        ReadNodes();
                        break;
                    case GeometryToken.Edges:
                        ReadEdges();
                        break;
                    case GeometryToken.Clusters:
                        ReadClusters();
                        break;
                    case GeometryToken.LayoutAlgorithmSettings:
                        Settings = ReadLayoutAlgorithmSettings(XmlReader); //todo . not tested
                        break;
                    case GeometryToken.LgLevels:
                        ReadLgLevels();
                        break;
                    case GeometryToken.LgSkeletonLevels:
                        ReadLgSkeletonLevels();
                        break;
                    case GeometryToken.End:
                    case GeometryToken.Graph:
                        if (XmlReader.NodeType == XmlNodeType.EndElement) {
                            done = true;
                            ReadEndElement();
                            break;
                        }
                        XmlRead();
                        break;
                    default: //ignore this element
                        XmlReader.Skip();
                        break;

                        //                        XmlReader.Skip();
                        //                        ReadHeader();
                        //                        if (TokenIs(GeometryToken.LayoutAlgorithmSettings))
                        //                            this.Settings = ReadLayoutAlgorithmSettings(XmlReader);
                        //                        ReadNodes();
                        //                        ReadClusters();
                        //                        ReadEdges();
                }
            } while (!done);
            _graph.BoundingBox = _graph.PumpTheBoxToTheGraphWithMargins();
        }

        void ReadLgLevels() {
            LgData lgData = new LgData(_graph);
            _graph.LgData = lgData;
            FillLgData(lgData);
            ReadEndElement();
        }

        void ReadLgSkeletonLevels() {
            XmlRead();
            ReadSkeletonLevels(_graph.LgData);
            ReadEndElement();
        }

        void FillLgData(LgData lgData) {

            XmlRead();
            if (TokenIs(GeometryToken.LgEdgeInfos))
            {
                ReadLgEdgeInfos(lgData);
            }

            if (TokenIs(GeometryToken.LgNodeInfos)) {
                ReadLgNodeInfos(lgData);
            }
            ReadLevels(lgData);
        }

        void ReadLgEdgeInfos(LgData lgData) {
            XmlRead();
            while (TokenIs(GeometryToken.LgEdgeInfo))
            {
                ReadLgEdgeInfo(lgData);
            }

            ReadEndElement();
        }

        void ReadLgEdgeInfo(LgData lgData) {
            string edgeId = GetAttribute(GeometryToken.EdgeId);
            Edge edge = idToEdges[edgeId];
            lgData.GeometryEdgesToLgEdgeInfos[edge] = new LgEdgeInfo(edge) {
                Rank = GetDoubleAttribute(GeometryToken.Rank),
                ZoomLevel = GetDoubleAttribute(GeometryToken.Zoomlevel)
            };
            XmlRead();
        }

        void ReadLevels(LgData lgData) {
            int zoomLevel = 1;
            while (GetElementTag() == GeometryToken.Level) {
                double dZoomLevel = GetDoubleAttributeOrDefault(GeometryToken.Zoomlevel, zoomLevel);
                ReadLevel(lgData, (int) dZoomLevel);
                zoomLevel = 2*(int) dZoomLevel;

            }
        }

        void ReadSkeletonLevels(LgData lgData) {
            int zoomLevel = 1;
            while (GetElementTag() == GeometryToken.SkeletonLevel) {
                double dZoomLevel = GetDoubleAttributeOrDefault(GeometryToken.Zoomlevel, zoomLevel);
                ReadSkeletonLevel(lgData, (int) dZoomLevel);
                zoomLevel = 2*(int) dZoomLevel;
            }
        }

        void ReadLevel(LgData lgData, int zoomLevel) {
            int levelNodeCount = GetIntAttribute(GeometryToken.NodeCountOnLevel);
            if (lgData.LevelNodeCounts == null) {
                lgData.LevelNodeCounts = [];
            }
            lgData.LevelNodeCounts.Add(levelNodeCount);
            LgLevel level = new LgLevel(zoomLevel, _graph);
            lgData.Levels.Add(level);
            XmlRead();
            Dictionary<string, Set<string>> edgeIdToEdgeRailsSet = [];
            ReadRailIdsPerEdgeIds(lgData, edgeIdToEdgeRailsSet);
            ReadRails(level);
            ReadEndElement();
            FillRailsOfEdges(level, edgeIdToEdgeRailsSet);
        }

        void ReadSkeletonLevel(LgData lgData, int zoomLevel) {
            LgSkeletonLevel level = new LgSkeletonLevel() {ZoomLevel = zoomLevel};
            lgData.SkeletonLevels.Add(level);

            if (XmlReader.IsEmptyElement) {
                XmlRead();
                return;
            }

            XmlRead();
            //ReadSkeletonRails(level);            
            ReadEndElement();
            //level.CreateRailTree();
        }

        void FillRailsOfEdges(LgLevel level, Dictionary<string, Set<string>> edgeIdToEdgeRailsSet) {
            foreach (KeyValuePair<string, Set<string>> edgeRails in edgeIdToEdgeRailsSet) {
                Edge edge = idToEdges[edgeRails.Key];
                Set<Rail> railSet = new Set<Rail>(edgeRails.Value.Where(s => s != "").Select(r => idToRails[r]));
                level._railsOfEdges[edge] = railSet;
            }
        }


        void ReadRailIdsPerEdgeIds(LgData lgData, Dictionary<string, Set<string>> edgeIdToEdgeRailsSet) {
            XmlRead();
            while (TokenIs(GeometryToken.EdgeRails))
            {
                ReadEdgeRailIds(lgData, edgeIdToEdgeRailsSet);
            }

            ReadEndElement();
        }

        void ReadEdgeRailIds(LgData lgData, Dictionary<string, Set<string>> edgeIdToEdgeRailsSet) {
            string edgeId = GetAttribute(GeometryToken.EdgeId);
            Set<string> railIdSet;
            edgeIdToEdgeRailsSet[edgeId] = railIdSet = [];
            string edgeRailsString = GetAttribute(GeometryToken.EdgeRails);
            LgEdgeInfo edgeInfo = lgData.GeometryEdgesToLgEdgeInfos[idToEdges[edgeId]];
            foreach (string railId in edgeRailsString.Split(' ')) {
                UpdateToRankedEdgeInfoForRail(railId, edgeInfo);
                railIdSet.Insert(railId);
            }
            XmlRead();
        }

        void UpdateToRankedEdgeInfoForRail(string railId, LgEdgeInfo edgeInfo) {
            if (railIdsToTopRankedEdgeInfo.TryGetValue(railId, out LgEdgeInfo? topRankeEdgeInfo))
            {
                if (topRankeEdgeInfo.Rank < edgeInfo.Rank)
                {
                    railIdsToTopRankedEdgeInfo[railId] = edgeInfo;
                }
            }
            else
            {
                railIdsToTopRankedEdgeInfo[railId] = edgeInfo;
            }
        }

        void ReadRails(LgLevel level) {
            //CheckToken(GeometryToken.Rails);
            //XmlRead();
            //while (TokenIs(GeometryToken.Rail)) {
            //    ReadRail(level);
            //}

            CheckToken(GeometryToken.Rails);
            if (XmlReader.IsEmptyElement) {
                XmlRead();
                return;
            }
            XmlRead();
            while (TokenIs(GeometryToken.Rail)) {
                ReadRail(level);
            }
            ReadEndElement();
        }

        void ReadSkeletonRails(LgSkeletonLevel level) {
            CheckToken(GeometryToken.Rails);
            if (XmlReader.IsEmptyElement) {
                XmlRead();
                return;
            }
            XmlRead();
            while (TokenIs(GeometryToken.Rail)) {
                ReadSkeletonRail(level);
            }
            ReadEndElement();
        }

        void ReadRail(LgLevel level) {
            string railId = GetAttribute(GeometryToken.Id);
            int zoomLevel = (int) GetDoubleAttribute(GeometryToken.Zoomlevel);
            double minPassigEdgeZoomLevel =
                (double) GetDoubleAttributeOrDefault(GeometryToken.MinPassingEdgeZoomLevel, zoomLevel);
            LgEdgeInfo topRankedEdgoInfo = GetTopRankedEdgeInfoOfRail(railId);
            Rail rail = ContinueReadingRail(topRankedEdgoInfo, zoomLevel, level);
            rail.MinPassingEdgeZoomLevel = minPassigEdgeZoomLevel;
            idToRails[railId] = rail;

        }

        void ReadSkeletonRail(LgSkeletonLevel level) {
            // do not save rails in skeleton level;
            return;
        }

        Rail ContinueReadingRail(LgEdgeInfo topRankedEdgoInfo, int zoomLevel, LgLevel level) {
            XmlRead();
            string pointString;
            if (TokenIs(GeometryToken.Arrowhead)) {
                Point arrowheadPosition = TryGetPointAttribute(GeometryToken.ArrowheadPosition);
                Point attachmentPoint = TryGetPointAttribute(GeometryToken.CurveAttachmentPoint);
                Arrowhead ah = new Arrowhead {
                    TipPosition = arrowheadPosition,
                    Length = (attachmentPoint - arrowheadPosition).Length
                };
                XmlRead();
                ReadEndElement();
                Rail rail = new Rail(ah, attachmentPoint, topRankedEdgoInfo, zoomLevel);
                SymmetricSegment tuple = new SymmetricSegment(arrowheadPosition, attachmentPoint);
                level._railDictionary[tuple] = rail;
                return rail;
            }

            if (TokenIs(GeometryToken.LineSegment)) {
                pointString = GetAttribute(GeometryToken.Points);
                Point[] linePoints = ParsePoints(pointString);
                Debug.Assert(linePoints.Length == 2);
                LineSegment ls = new LineSegment(linePoints[0], linePoints[1]);
                XmlRead();
                ReadEndElement();
                Rail rail = new Rail(ls, topRankedEdgoInfo, zoomLevel);
                SymmetricSegment tuple = new SymmetricSegment(ls.Start, ls.End);
                level._railDictionary[tuple] = rail;
                level._railTree.Add(ls.BoundingBox, rail);
                return rail;
            }
            if (TokenIs(GeometryToken.CubicBezierSegment)) {
                pointString = GetAttribute(GeometryToken.Points);
                Point[] controlPoints = ParsePoints(pointString);
                Debug.Assert(controlPoints.Length == 4);
                CubicBezierSegment bs = new CubicBezierSegment(controlPoints[0], controlPoints[1], controlPoints[2], controlPoints[3]);
                XmlRead();
                ReadEndElement();
                Rail rail = new Rail(bs, topRankedEdgoInfo, zoomLevel);
                SymmetricSegment tuple = new SymmetricSegment(bs.Start, bs.End);
                level._railDictionary[tuple] = rail;
                return rail;
            }
            throw new Exception();
        }

        LgEdgeInfo GetTopRankedEdgeInfoOfRail(string railId) {
            if (!railIdsToTopRankedEdgeInfo.ContainsKey(railId))
            {
                return null;
            }

            return railIdsToTopRankedEdgeInfo[railId];
        }

        LgEdgeInfo GetTopRankedEdgeInfoOfSkeletonRail(string railId) {
            if (railIdsToTopRankedEdgeInfo.ContainsKey(railId))
            {
                return railIdsToTopRankedEdgeInfo[railId];
            }

            return null;
        }

        void ReadLgNodeInfos(LgData lgData) {
            if (XmlReader.IsEmptyElement)
            {
                return;
            }

            lgData.GeometryNodesToLgNodeInfos = [];
            lgData.SortedLgNodeInfos = [];
            XmlRead();
            while (TokenIs(GeometryToken.LgNodeInfo))
            {
                ReadLgNodeInfo(lgData);
            }

            ReadEndElement();
        }

        void ReadLgNodeInfo(LgData lgData) {
            string nodeId = GetAttribute(GeometryToken.Id);
            LgNodeInfo nodeInfo = new LgNodeInfo(nodeIdToNodes[nodeId]) {
                Rank = GetDoubleAttribute(GeometryToken.Rank),
                ZoomLevel = GetDoubleAttribute(GeometryToken.Zoomlevel),
                LabelVisibleFromScale = GetDoubleAttributeOrDefault(GeometryToken.LabelVisibleFromScale, 1.0),
                LabelWidthToHeightRatio = GetDoubleAttributeOrDefault(GeometryToken.LabelWidthToHeightRatio, 1.0),
                LabelOffset = TryGetPointAttribute(GeometryToken.LabelOffset)
            };
            lgData.SortedLgNodeInfos.Add(nodeInfo);
            lgData.GeometryNodesToLgNodeInfos[nodeIdToNodes[nodeId]] = nodeInfo;
            XmlRead();
        }

        GeometryToken GetElementTag() {
            if (XmlReader.NodeType == XmlNodeType.EndElement &&
                XmlReader.Name == "graph")
            {
                return GeometryToken.End;
            }

            if (XmlReader.ReadState == ReadState.EndOfFile)
            {
                return GeometryToken.Graph;
            }

            if (Enum.TryParse(XmlReader.Name, true, out             GeometryToken token))
            {
                return token;
            }

            return GeometryToken.Unknown;
        }

        void ReadClusters() {
            XmlRead();
            while (TokenIs(GeometryToken.Cluster))
            {
                ReadCluster();
            }

            FleshOutClusters();
            Set<Cluster> rootClusterSet = new Set<Cluster>();
            foreach (Cluster? cluster in stringToClusters.Values.Select(c => c.Cluster))
            {
                if (cluster.ClusterParents == null || !cluster.ClusterParents.Any())
                {
                    rootClusterSet.Insert(cluster);
                }
            }

            if (rootClusterSet.Count == 1)
            {
                _graph.RootCluster = rootClusterSet.First();
            }
            else {
                _graph.RootCluster.AddRangeOfCluster(rootClusterSet);
            }

            if (!XmlReader.IsStartElement())
            {
                ReadEndElement();
            }
        }

        void FleshOutClusters() {
            foreach (ClusterWithChildLists clusterWithLists in stringToClusters.Values) {
                Cluster cl = clusterWithLists.Cluster;
                foreach (string i in clusterWithLists.ChildClusters)
                {
                    cl.AddCluster(stringToClusters[i].Cluster);
                }

                foreach (string i in clusterWithLists.ChildNodes)
                {
                    cl.AddNode(nodeIdToNodes[i]);
                }
            }
        }

        void ReadCluster() {
            Cluster cluster = new Cluster {RectangularBoundary = new RectangularClusterBoundary()};
            ClusterWithChildLists clusterWithChildLists = new ClusterWithChildLists(cluster);
            cluster.Barycenter = TryGetPointAttribute(GeometryToken.Barycenter);
            string clusterId = GetAttribute(GeometryToken.Id);
            stringToClusters[clusterId] = clusterWithChildLists;
            ReadChildClusters(clusterWithChildLists.ChildClusters);
            ReadChildNodes(clusterWithChildLists.ChildNodes);
            XmlRead();
            switch (NameToToken()) {
                case GeometryToken.ICurve:
                    cluster.BoundaryCurve = ReadICurve();
                    break;
                case GeometryToken.Curve:
                    cluster.BoundaryCurve = ReadCurve();
                    XmlRead();
                    break;
                case GeometryToken.Rect:
                    cluster.BoundaryCurve = ReadRect();
                    XmlRead();
                    break;
            }

            if (XmlReader.NodeType != XmlNodeType.EndElement)
            {
                cluster.RectangularBoundary = ReadClusterRectBoundary();
            }

            ReadEndElement();
        }

        RectangularClusterBoundary ReadClusterRectBoundary() {
            RectangularClusterBoundary recClBnd = new RectangularClusterBoundary {
                LeftMargin = GetDoubleAttribute(GeometryToken.LeftMargin),
                RightMargin = GetDoubleAttribute(GeometryToken.RightMargin),
                TopMargin = GetDoubleAttribute(GeometryToken.TopMargin),
                BottomMargin = GetDoubleAttribute(GeometryToken.BottomMargin)
            };

            if (GetAttribute(GeometryToken.DefaultLeftMargin) != null) {
                double defaultLeftMargin = GetDoubleAttribute(GeometryToken.DefaultLeftMargin);
                double defaultRightMargin = GetDoubleAttribute(GeometryToken.DefaultRightMargin);
                double defaultTopMargin = GetDoubleAttribute(GeometryToken.DefaultBottomMargin);
                double defaultBottomMargin = GetDoubleAttribute(GeometryToken.DefaultBottomMargin);
                recClBnd.StoreDefaultMargin(defaultLeftMargin, defaultRightMargin, defaultBottomMargin, defaultTopMargin);
            }

            recClBnd.GenerateFixedConstraints = GetBoolAttributeOrDefault(GeometryToken.GenerateFixedConstraints, false);
            recClBnd.GenerateFixedConstraintsDefault =
                GetBoolAttributeOrDefault(GeometryToken.GenerateFixedConstraintsDefault, false);

            recClBnd.MinHeight = GetDoubleAttribute(GeometryToken.MinNodeHeight);
            recClBnd.MinWidth = GetDoubleAttribute(GeometryToken.MinNodeWidth);


            XmlRead();
            ReadRectParams(out double left, out double bottom, out double w, out double h, out double rx, out double ry);
            recClBnd.Rect = new Rectangle(left, bottom, new Point(w, h));
            recClBnd.RadiusX = rx;
            recClBnd.RadiusY = ry;


            recClBnd.RightBorderInfo = ReadBorderInfo(GeometryToken.RightBorderInfo);
            recClBnd.LeftBorderInfo = ReadBorderInfo(GeometryToken.LeftBorderInfo);
            recClBnd.TopBorderInfo = ReadBorderInfo(GeometryToken.TopBorderInfo);
            recClBnd.BottomBorderInfo = ReadBorderInfo(GeometryToken.BottomBorderInfo);
            XmlRead();
            ReadEndElement();
            return recClBnd;
        }

        BorderInfo ReadBorderInfo(GeometryToken token) {
            XmlRead();
            CheckToken(token);
            BorderInfo bi = new BorderInfo {
                InnerMargin = GetDoubleAttribute(GeometryToken.InnerMargin),
                FixedPosition = GetDoubleAttribute(GeometryToken.FixedPosition),
                Weight = GetDoubleAttribute(GeometryToken.Weight)
            };
            return bi;
        }

        void ReadChildClusters(List<string> childClusters) {
            string clusterIds = GetAttribute(GeometryToken.ChildClusters);
            if (string.IsNullOrEmpty(clusterIds))
            {
                return;
            }

            childClusters.AddRange(clusterIds.Split(' '));
        }

        void ReadChildNodes(List<string> childNodes) {
            string nodeIds = GetAttribute(GeometryToken.ChildNodes);
            if (string.IsNullOrEmpty(nodeIds))
            {
                return;
            }

            childNodes.AddRange(nodeIds.Split(' '));
        }

        void ReadEdges() {
            CheckToken(GeometryToken.Edges);
            XmlRead();
            while (TokenIs(GeometryToken.Edge))
            {
                ReadEdge();
            }

            ReadEndElement();
        }

        void ReadEdge() {
            CheckToken(GeometryToken.Edge);
            Node s = ReadSourceNode();
            Node t = ReadTargetNode();
            Edge edge = new Edge(s, t) {
                Separation = (int) GetDoubleAttributeOrDefault(GeometryToken.Separation, 1),
                LineWidth = GetDoubleAttributeOrDefault(GeometryToken.LineWidth, 1),
                Weight = (int) GetDoubleAttributeOrDefault(GeometryToken.Weight, 1),

            };
            string id = GetAttribute(GeometryToken.Id);
            if (id != null) {
                Debug.Assert(idToEdges.ContainsKey(id) == false);
                idToEdges[id] = edge;
            }
            else {
                Debug.Assert(idToEdges.Count == 0); // we consistently should have no ids or unique id per edge
            }

            EdgeList.Add(edge);
            ReadArrowheadAtSource(edge);
            ReadArrowheadAtTarget(edge);
            ReadLabelFromAttribute(edge);
            bool breakTheLoop = false;
            //edge.UnderlyingPolyline = ReadUnderlyingPolyline();
            _graph.Edges.Add(edge);
            if (XmlReader.IsEmptyElement) {
                XmlReader.Skip();
                return;
            }
            XmlRead();
            do {
                GeometryToken token = GetElementTag();
                switch (token) {
                    case GeometryToken.Curve:
                        edge.Curve = ReadICurve();
                        break;
                    case GeometryToken.LineSegment:
                        edge.Curve = ReadLineSeg();
                        break;
                    case GeometryToken.Edge:
                        ReadEndElement();
                        breakTheLoop = true;
                        break;
                    case GeometryToken.Polyline:

                        break;
                    default:
                        breakTheLoop = true;
                        break;
                }
                if (!breakTheLoop)
                {
                    XmlRead();
                }
            } while (!breakTheLoop);
            //XmlReader.Skip();
        }



        void ReadArrowheadAtSource(Edge edge) {
            string str = GetAttribute(GeometryToken.As);
            Arrowhead? arrowhead =
                edge.EdgeGeometry.SourceArrowhead = str != null ? new Arrowhead {TipPosition = ParsePoint(str)} : null;
            if (arrowhead != null)
            {
                arrowhead.Length = GetDoubleAttributeOrDefault(GeometryToken.Asl, Arrowhead.DefaultArrowheadLength);
            }
            else {
                str = GetAttribute(GeometryToken.Asl);
                if (str != null)
                {
                    edge.EdgeGeometry.SourceArrowhead = new Arrowhead {Length = ParseDouble(str)};
                }
            }
        }

        Point ParsePoint(string str) {
            string[] xy = str.Split(' ').ToArray();
            Debug.Assert(xy.Length == 2);
            if (double.TryParse(xy[0], out double x) && double.TryParse(xy[1], out double y))
            {
                return new Point(x, y);
            }

            Error("invalid point format" + str);
            return new Point();
        }

        Point[] ParsePoints(string str) {
            string[] tokens = str.Split(' ');
            Debug.Assert(tokens.Length%2 == 0);
            Point[] ret = new Point[tokens.Length/2];
            for (int i = 0; i < tokens.Length - 1; i += 2) {
                if (double.TryParse(tokens[i], out double x) && double.TryParse(tokens[i + 1], out double y))
                {
                    ret[i / 2] = new Point(x, y);
                }
                else
                {
                    Error("invalid point format" + str);
                }
            }
            return ret;
        }

        void ReadArrowheadAtTarget(Edge edge) {
            string str = GetAttribute(GeometryToken.At);
            Arrowhead? arrowhead =
                edge.EdgeGeometry.TargetArrowhead = str != null ? new Arrowhead {TipPosition = ParsePoint(str)} : null;
            if (arrowhead != null)
            {
                arrowhead.Length = GetDoubleAttributeOrDefault(GeometryToken.Atl, Arrowhead.DefaultArrowheadLength);
            }
            else {
                str = GetAttribute(GeometryToken.Atl);
                if (str != null)
                {
                    edge.EdgeGeometry.TargetArrowhead = new Arrowhead {Length = ParseDouble(str)};
                }
            }
        }

        void ReadLabelFromAttribute(GeometryObject geomObj) {
            if (!TryGetAttribute(GeometryToken.Label, out string? str))
            {
                return;
            }

            Label label = new Label(geomObj);
            ParseLabel(str, out Point center, out double width, out double height);
            label.Center = center;
            label.Width = width;
            label.Height = height;

            if (geomObj is Edge edge)
            {
                edge.Label = label;
            }
        }


        void ParseLabel(string str, out Point center, out double width, out double height) {
            string[] ss = str.Split(' ');
            Debug.Assert(ss.Length == 4);

            center = new Point(ParseDouble(ss[0]), ParseDouble(ss[1]));
            width = ParseDouble(ss[2]);
            height = ParseDouble(ss[3]);
        }

        double ParseDouble(string s) {
            if (double.TryParse(s, out double ret))
            {
                return ret;
            }

            Error(" cannot parse double " + s);
            return 0;
        }

/*
        SmoothedPolyline ReadUnderlyingPolyline() {
            CheckToken(GeometryToken.UnderlyingPolyline);
            XmlRead();
            if (ReadBooleanElement(GeometryToken.UnderlyingPolylineIsNull)) {
                ReadEndElement();
                return null;
            }

            Site s = ReadSite();
            var poly = new SmoothedPolyline(s);
            while (TokenIs(GeometryToken.PolylineSite)) {
                Site ns = ReadSite();
                s.Next = ns;
                ns.Previous = s;
                s = ns;
            }
            ReadEndElement();
            return poly;
        }
*/

/*
        Site ReadSite() {
            CheckToken(GeometryToken.PolylineSite);
            XmlRead();
            var s = new Site {
                                 PreviousBezierSegmentFitCoefficient = ReadDoubleElement(GeometryToken.SiteK),
                                 NextBezierSegmentFitCoefficient = ReadDoubleElement(GeometryToken.SiteK),
                                 Point = ReadPointElement(GeometryToken.SiteV)
                             };
            ReadEndElement();
            return s;
        }
*/


        Node ReadTargetNode() {
            string targetId = GetMustAttribute(GeometryToken.T);
            return GetNodeOrClusterById(targetId);
        }

        Node ReadSourceNode() {
            string id = GetMustAttribute(GeometryToken.S);
            return GetNodeOrClusterById(id);
        }

        Node GetNodeOrClusterById(string id) {
            if (nodeIdToNodes.TryGetValue(id, out Node? ret))
            {
                return ret;
            }

            return stringToClusters[id].Cluster;

        }

        void ReadNodes() {
            if (XmlReader.IsEmptyElement)
            {
                return;
            }

            XmlRead();
            while (TokenIs(GeometryToken.Node))
            {
                ReadNode();
            }

            ReadEndElement();
        }

        internal const double NodeDefaultPadding = 1;

        void ReadNode() {
            string nodeId = GetMustAttribute(GeometryToken.Id);
            double nodePadding = GetDoubleAttributeOrDefault(GeometryToken.Padding, NodeDefaultPadding);
            XmlRead();
            Node node = new Node(ReadICurve()) {Padding = nodePadding, UserData = nodeId};
            if (node.BoundaryCurve == null)
            {
                throw new InvalidOperationException();
            }

            _graph.Nodes.Add(node);
            XmlReader.Skip();
            ReadEndElement();
            nodeIdToNodes[nodeId] = node;
        }

        string GetAttribute(GeometryToken token) => XmlReader.GetAttribute(GeometryGraphWriter.FirstCharToLower(token));

        bool TryGetAttribute(GeometryToken token, out string val) => (val = GetAttribute(token)) != null;

        string GetMustAttribute(GeometryToken token) {
            string s = GeometryGraphWriter.FirstCharToLower(token);
            string? ret = XmlReader.GetAttribute(GeometryGraphWriter.FirstCharToLower(token));
            if (ret != null)
            {
                return ret;
            }

            Error("attribute " + s + " not found");
            return null;
        }

        int GetIntAttribute(GeometryToken token) {
            string? val = GetAttribute(token);
            if (val == null)
            {
                Error(string.Format("attribute {0} not found", token));
            }

            if (int.TryParse(val, out int ret))
            {
                return ret;
            }

            Error("cannot parse an int value " + val);
            return 0;
        }

        double GetDoubleAttribute(GeometryToken token) {
            string? val = GetAttribute(token);
            if (val == null)
            {
                Error(string.Format("attribute {0} not found", token));
            }

            if (double.TryParse(val, out double ret))
            {
                return ret;
            }

            Error("cannot parse an int value " + val);
            return 0;
        }

        double GetDoubleAttributeOrDefault(GeometryToken token, double defaultVal) {
            string val = GetAttribute(token);
            if (val == null)
            {
                return defaultVal;
            }

            if (double.TryParse(val, out double ret))
            {
                return ret;
            }

            Error("cannot parse a double value " + val);
            return 0;
        }

        bool GetBoolAttributeOrDefault(GeometryToken token, bool defaultVal) {
            string val = GetAttribute(token);
            if (val == null)
            {
                return defaultVal;
            }

            if (bool.TryParse(val, out bool ret))
            {
                return ret;
            }

            Error("cannot parse a bool value " + val);
            return false;
        }

        int GetIntAttributeOrDefault(GeometryToken token, int defaultVal) {
            string val = GetAttribute(token);
            if (val == null)
            {
                return defaultVal;
            }

            if (int.TryParse(val, out int ret))
            {
                return ret;
            }

            Error("cannot parse a bool value " + val);
            return 0;
        }


        Point TryGetPointAttribute(GeometryToken token) {
            string val = GetAttribute(token);
            return val == null ? new Point() : ParsePoint(val);
        }

        void Error(string msg) => throw new InvalidOperationException(msg + ";" + GetPositionInfo());

        ICurve ReadICurve() {
            switch (NameToToken()) {
                case GeometryToken.Curve:
                    return ReadCurve();
                case GeometryToken.Ellipse:
                    return ReadEllipse();
                case GeometryToken.Rect:
                    return ReadRect();
                case GeometryToken.Polygon:
                    return ReadPolygon();
                    //            if (hasCurve) {
                    //                XmlRead();
                    //                ICurve ret = null;
                    //                if (TokenIs(GeometryToken.Ellipse))
                    //                    ret = ReadEllipse();
                    //                else if (TokenIs(GeometryToken.Curve))
                    //                    ret = ReadCurve();
                    //                else if (TokenIs(GeometryToken.LineSegment))
                    //                    ret = ReadLineSeg();
                    //                else if (TokenIs(GeometryToken.CubicBezierSegment))
                    //                    ret = ReadCubicBezierSeg();
                    //                else if (TokenIs(GeometryToken.Polyline))
                    //                    ret = ReadPolyline();
            }
            return null;
        }

        ICurve ReadPolygon() {
            string pointString = GetMustAttribute(GeometryToken.Points);
            string[] t = pointString.Split(' ');
            if (t.Length == 0 || t.Length%2 != 0)
            {
                Error("invalid input for the polygon");
            }

            Polyline poly = new Polyline {Closed = true};
            for (int i = 0; i < t.Length; i += 2)
            {
                poly.AddPoint(new Point(ParseDouble(t[i]), ParseDouble(t[i + 1])));
            }

            return poly;
        }

        ICurve ReadRect() {
            ReadRectParams(out double x, out double y, out double width, out double height, out double rx, out double ry);
            Rectangle box = new Rectangle(x, y, x + width, y + height);
            return new RoundedRect(box, rx, ry);
        }

        void ReadRectParams(out double x, out double y, out double width, out double height, out double rx,
            out double ry) {
            x = GetDoubleAttributeOrDefault(GeometryToken.X, 0);
            y = GetDoubleAttributeOrDefault(GeometryToken.Y, 0);
            width = ParseDouble(GetMustAttribute(GeometryToken.Width));
            height = ParseDouble(GetMustAttribute(GeometryToken.Height));
            rx = GetDoubleAttributeOrDefault(GeometryToken.Rx, 0);
            ry = GetDoubleAttributeOrDefault(GeometryToken.Ry, 0);
        }

        ICurve ReadCurve() {
            if (XmlReader.MoveToFirstAttribute()) {
                GeometryToken token = NameToToken();
                switch (token) {
                    case GeometryToken.CurveData:
                        return ParseCurve(XmlReader.Value);
                }
                throw new InvalidOperationException();
            }
            Error("No boundary curve is defined");
            return null;
        }

        GeometryToken NameToToken() {
            if (Enum.TryParse(XmlReader.Name, true, out GeometryToken token))
            {
                return token;
            }

            Error("cannot parse " + XmlReader.Name);
            return GeometryToken.Error;
        }

        ICurve ParseCurve(string curveData) {
            Curve curve = new Curve();
            CurveStream curveStream = new CurveStream(curveData);
            Point currentPoint = new Point();
            do {
                CurveStreamElement curveStreamElement = curveStream.GetNextCurveStreamElement();
                if (curveStreamElement == null)
                {
                    return curve;
                }

                if (curveStreamElement is not CharStreamElement charStreamElement)
                {
                    Error("wrong formatted curve string " + curveStreamElement);
                    return null;
                }
                AddCurveSegment(curveStream, charStreamElement.Char, curve, ref currentPoint);
            } while (true);
        }

        void AddCurveSegment(CurveStream curveStream, char c, Curve curve, ref Point currentPoint) {
            switch (c) {
                case 'M': //moveto
                    currentPoint = GetNextPointFromCurveData(curveStream);
                    break;
                case 'm': //relative moveto
                    throw new NotImplementedException();
                case 'Z':
                case 'z': //closepath
                    if (curve.Segments.Count == 0)
                    {
                        Error("the curve is too short");
                    }

                    curve.AddSegment(new LineSegment(currentPoint, currentPoint = curve.Start));
                    break;
                case 'L': //lineto
                    ProceedWithLines(curve, curveStream, ref currentPoint);
                    break;
                case 'l': //lineto relative
                    throw new NotImplementedException();
                case 'H': //lineto horizontal
                    throw new NotImplementedException();
                case 'h': //lineto horizontal relative
                    throw new NotImplementedException();
                case 'V': //lineto vertical
                    throw new NotImplementedException();
                case 'v': //lineto vertical relative
                    throw new NotImplementedException();
                case 'C': //cubic Bezier
                    ProceedWithCubicBeziers(curve, curveStream, ref currentPoint);
                    break;
                case 'c': //cubic Bezier relative
                    throw new NotImplementedException();
                case 'S': //cubic Bezier shorthand
                    throw new NotImplementedException();
                case 's': //cubic Bezier relative shorthand
                    throw new NotImplementedException();
                case 'Q': //quadratic Bezier
                    throw new NotImplementedException();
                case 'q': //quadratic Bezier relative
                    throw new NotImplementedException();
                case 'T': //quadratic Bezier shorthand
                    throw new NotImplementedException();
                case 't': //quadratic Bezier relative shorthand
                    throw new NotImplementedException();
                case 'A': //elleptical arc
                    ReadEllepticalArc(curve, curveStream, ref currentPoint);
                    break;
                case 'a': //eleptical arc relative
                    throw new NotImplementedException();
                default:
                    Error("unknown character " + c);
                    break;
            }
        }

        void ProceedWithCubicBeziers(Curve curve, CurveStream curveStream, ref Point currentPoint) {
            do {
                curve.AddSegment(new CubicBezierSegment(currentPoint, GetNextPointFromCurveData(curveStream),
                    GetNextPointFromCurveData(curveStream), currentPoint = GetNextPointFromCurveData(curveStream)));
            } while (curveStream.PickNextCurveStreamElement() is DoubleStreamElement);
        }

        void ReadEllepticalArc(Curve curve, CurveStream curveStream, ref Point currentPoint) => curve.AddSegment(ReadEllepticalArc(curveStream, ref currentPoint));

        ICurve ReadEllepticalArc(CurveStream curveStream, ref Point currentPoint) {
            /*
            var rx = "A"+DoubleToString(ellipse.AxisA.Length);
            var ry = DoubleToString(ellipse.AxisB.Length);
            var xAxisRotation = DoubleToString(180*Point.Angle(new Point(1, 0), ellipse.AxisA)/Math.PI);
            var largeArcFlag = Math.Abs(ellipse.ParEnd - ellipse.ParStart) >= Math.PI ? "1" : "0";
            var sweepFlag = ellipse.ParEnd > ellipse.ParStart ? "1" : "0"; //it happens because of the y-axis orientation down in SVG
            var endPoint=PointToString(ellipse.End);
            return string.Join(" ", new[] {rx, ry, xAxisRotation, largeArcFlag, sweepFlag, endPoint});
            var endPoint = GetCurveDataPoint(curveStream);
             */
            double rx = GetNextDoubleFromCurveData(curveStream);
            double ry = GetNextDoubleFromCurveData(curveStream);
            double xAxisRotation = GetNextDoubleFromCurveData(curveStream)/180*Math.PI;
            int largeArcFlag = (int) GetNextDoubleFromCurveData(curveStream);
            int sweepFlag = (int) GetNextDoubleFromCurveData(curveStream);
            Point endPoint = GetNextPointFromCurveData(curveStream);
            //figure out the transform to the circle
            //then solve this problem on the circle
            if (ApproximateComparer.Close(rx, 0) || ApproximateComparer.Close(ry, 0))
            {
                Error("ellipseArc radius is too small");
            }

            double yScale = rx/ry;
            PlaneTransformation rotationMatrix = PlaneTransformation.Rotation(-xAxisRotation);
            PlaneTransformation scaleMatrix = new PlaneTransformation(1, 0, 0, 0, yScale, 0);
            PlaneTransformation transform = scaleMatrix*rotationMatrix;
            Point start = transform*currentPoint;
            currentPoint = endPoint;
            Point end = transform*endPoint;
            GetArcCenterAndAngles(rx, largeArcFlag, sweepFlag, start, end, out Point center, out double startAngle, out double endAngle,
                out Point axisY);
            PlaneTransformation inverted = transform.Inverse;
            center = inverted*center;
            PlaneTransformation rotation = PlaneTransformation.Rotation(xAxisRotation);
            Point axisX = rotation*new Point(rx, 0);
            axisY = rotation*(axisY/yScale);
            Ellipse ret = new Ellipse(startAngle, endAngle, axisX, axisY, center);

            Debug.Assert(ApproximateComparer.Close(ret.End, endPoint));
            return ret;
        }

        void GetArcCenterAndAngles(double r, int largeArcFlag, int sweepFlag, Point start, Point end, out Point center,
            out double startAngle, out double endAngle, out Point axisY) {
            Point d = end - start;
            double dLenSquared = d.LengthSquared; //use it to get more precision
            double dLen = d.Length;
            //            if(dLen<r-ApproximateComparer.DistanceEpsilon)
            //                Error("arc radius is too small");

            Point middle = (start + end)/2;

            //the circle center belongs to the perpendicular to d passing through 'middle'
            d /= dLen;
            Point perp = new Point(d.Y, -d.X)*Math.Sqrt(r*r - dLenSquared/4);
            center = sweepFlag == 1 && largeArcFlag == 0 || sweepFlag == 0 && largeArcFlag == 1
                ? middle - perp
                : middle + perp;
            Point axisX = new Point(r, 0);
            axisY = sweepFlag == 1 ? new Point(0, r) : new Point(0, -r);
            startAngle = Point.Angle(axisX, start - center);
            if (sweepFlag == 0)
            {
                startAngle = 2*Math.PI - startAngle;
            }

            endAngle = Point.Angle(axisX, end - center);
            if (sweepFlag == 0)
            {
                endAngle = 2*Math.PI - endAngle;
            }

            if (ApproximateComparer.Close(endAngle, startAngle) && largeArcFlag == 1)
            {
                endAngle += 2*Math.PI;
            }
            else if (endAngle < startAngle)
            {
                endAngle += 2*Math.PI;
            }
        }

        void ProceedWithLines(Curve curve, CurveStream curveStream, ref Point currentPoint) {
            do {
                curve.AddSegment(new LineSegment(currentPoint, currentPoint = GetNextPointFromCurveData(curveStream)));
            } while (curveStream.PickNextCurveStreamElement() is DoubleStreamElement);
        }

        Point GetNextPointFromCurveData(CurveStream curveStream) => new Point(GetNextDoubleFromCurveData(curveStream), GetNextDoubleFromCurveData(curveStream));

        double GetNextDoubleFromCurveData(CurveStream curveStream) {
            CurveStreamElement? a = curveStream.GetNextCurveStreamElement();
            if (a == null)
            {
                Error("cannot parse curveData");
            }

            DoubleStreamElement? d = a as DoubleStreamElement;
            if (d == null)
            {
                Error("cannot parse curveData");
            }
            // ReSharper disable PossibleNullReferenceException
            return d.Double;
// ReSharper restore PossibleNullReferenceException
        }


/*
        ICurve ReadPolyline() {
            XmlRead();
            var poly = new Polyline {
                Closed = ReadBooleanElement(GeometryToken.Closed)
            };
            CheckToken(GeometryToken.PolylinePoints);
            XmlRead();
            while (TokenIs(GeometryToken.Point))
                poly.AddPoint(ReadPoint());
            ReadEndElement();
            ReadEndElement();
            return poly;
        }
*/

        ICurve ReadLineSeg() {
            CheckToken(GeometryToken.LineSegment);
            string str = GetAttribute(GeometryToken.Points);
            string[] ss = str.Split(' ');
            if (ss.Length != 4)
            {
                Error("wrong number of points in LineSegment");
            }

            double[] ds = ss.Select(ParseDouble).ToArray();
            return new LineSegment(new Point(ds[0], ds[1]), new Point(ds[2], ds[3]));
        }

        ICurve ReadEllipse() {
            double cx = ParseDouble(GetMustAttribute(GeometryToken.Cx));
            double cy = ParseDouble(GetMustAttribute(GeometryToken.Cy));
            double rx = ParseDouble(GetMustAttribute(GeometryToken.Rx));
            double ry = ParseDouble(GetMustAttribute(GeometryToken.Ry));
            return new Ellipse(rx, ry, new Point(cx, cy));
        }


        bool TokenIs(GeometryToken t) => XmlReader.IsStartElement(GeometryGraphWriter.FirstCharToLower(t)) ||
                   XmlReader.IsStartElement(t.ToString());


        /*
                void ReadTransform(PlaneTransformation transform) {
                    if (TokenIs(GeometryToken.Transform)) {
                        XmlRead();
                        for (int i = 0; i < 2; i++)
                            for (int j = 0; j < 3; j++) {
                                CheckToken(GeometryToken.TransformElement);
                                MoveToContent();
                                transform[i, j] = ReadElementContentAsDouble();
                            }
                        XmlRead();
                    } else {
                        //set the unit transform
                        transform[0, 0] = 1;
                        transform[0, 1] = 0;
                        transform[0, 2] = 0;
                        transform[1, 0] = 0;
                        transform[1, 1] = 1;
                        transform[1, 2] = 0;
                    }
                }
        */


        void MoveToContent() => XmlReader.MoveToContent();


        /*
                /// <summary>
                /// reads the layout algorithm settings
                /// </summary>
                LayoutAlgorithmSettings ReadLayoutAlgorithmSettings(XmlReader reader) {
                    LayoutAlgorithmSettings layoutSettings = null;
                    CheckToken(reader, GeometryToken.LayoutAlgorithmSettings);
                    if (reader.IsEmptyElement) {
                        reader.Read();
                        return null;
                    }
                    reader.Read();
                    var edgeRoutingMode = (EdgeRoutingMode)ReadIntElement(reader, GeometryToken.EdgeRoutingMode);
                    bool useSpanner = ReadBooleanElement(GeometryToken.UseSparseVisibilityGraph);
                    bool useKdHull = ReadBooleanElement(GeometryToken.UseKdHull);
                    if (XmlReader.NodeType == XmlNodeType.EndElement) { //todo - support fastincremental settings
                        layoutSettings = new FastIncrementalLayoutSettings();
                        EdgeRoutingSettings routingSettings = layoutSettings.EdgeRoutingSettings;
                        routingSettings.UseKdHullForRouting = useKdHull;
                        routingSettings.UseSparseVisibilityGraph = useSpanner;
                        routingSettings.EdgeRoutingMode = edgeRoutingMode;
                    } else {
                        var token = (GeometryToken)Enum.Parse(typeof(GeometryToken), reader.ReadElementContentAsString(), false);
                        if (token == GeometryToken.SugiyamaLayoutSettings) {
                            layoutSettings = ReadSugiyamaLayoutSettings(reader, useKdHull, useSpanner, edgeRoutingMode);
                        } else if (token == GeometryToken.MdsLayoutSettings) {
                            var mds = new MdsLayoutSettings();
                            EdgeRoutingSettings routingSettings = mds.EdgeRoutingSettings;
                            routingSettings.EdgeRoutingMode = edgeRoutingMode;
                            routingSettings.UseSparseVisibilityGraph = useSpanner;
                            routingSettings.UseKdHullForRouting = useKdHull;

                            layoutSettings = mds;
        #if REPORTING
                            mds.Reporting = ReadBooleanElement(reader, GeometryToken.Reporting);
        #endif
                            mds.Exponent = ReadDoubleElement(reader, GeometryToken.Exponent);
                            mds.majorizationMaxIter = ReadIntElement(reader, GeometryToken.majorizationMaxIter);
                            mds.PivotNumber = ReadIntElement(reader, GeometryToken.PivotNumber);
                            mds.RotationAngle = ReadDoubleElement(reader, GeometryToken.RotationAngle);
                            mds.ScaleX = ReadDoubleElement(reader, GeometryToken.ScaleX);
                            mds.ScaleY = ReadDoubleElement(reader, GeometryToken.ScaleY);
                        } else//todo - write a reader and a writer for FastIncrementalLayoutSettings 
                            throw new NotImplementedException();
                    }
                    reader.ReadEndElement();

                    return layoutSettings;
                }
        */

        /*
                LayoutAlgorithmSettings ReadSugiyamaLayoutSettings(XmlReader reader, bool useKdHull, bool useSpanner, EdgeRoutingMode edgeRoutingMode) {
                    LayoutAlgorithmSettings layoutSettings;
                    var sugiyama = new SugiyamaLayoutSettings();
                    EdgeRoutingSettings routingSettings = sugiyama.EdgeRoutingSettings;
                    routingSettings.EdgeRoutingMode = edgeRoutingMode;
                    routingSettings.UseSparseVisibilityGraph = useSpanner;
                    routingSettings.UseKdHullForRouting = useKdHull;

                    layoutSettings = sugiyama;

                    sugiyama.MinNodeWidth = ReadDoubleElement(GeometryToken.MinNodeWidth);
                    sugiyama.MinNodeHeight = ReadDoubleElement(GeometryToken.MinNodeHeight);
                    ReadAspectRatio(sugiyama);
                    ReadNodeSeparation(sugiyama);

        #if REPORTING
                    sugiyama.Reporting = ReadBooleanElement(reader, GeometryToken.Reporting);
        #endif
                    sugiyama.RandomSeedForOrdering = ReadIntElement(reader, GeometryToken.RandomSeedForOrdering);
                    sugiyama.NoGainAdjacentSwapStepsBound = ReadIntElement(reader, GeometryToken.NoGainStepsBound);
                    sugiyama.MaxNumberOfPassesInOrdering = ReadIntElement(reader,
                                                                          GeometryToken.MaxNumberOfPassesInOrdering);
                    sugiyama.RepetitionCoefficientForOrdering = ReadIntElement(reader,
                                                                               GeometryToken.
                                                                                   RepetitionCoefficientForOrdering);
                    sugiyama.GroupSplit = ReadIntElement(reader, GeometryToken.GroupSplit);
                    sugiyama.LabelCornersPreserveCoefficient = ReadDoubleElement(reader,
                                                                                 GeometryToken.
                                                                                     LabelCornersPreserveCoefficient);
                    sugiyama.BrandesThreshold = ReadIntElement(reader, GeometryToken.BrandesThreshold);
                    sugiyama.LayerSeparation = ReadDoubleElement(reader, GeometryToken.LayerSeparation);
                    var transform = new PlaneTransformation();
                    ReadTransform(transform);
                    return layoutSettings;
                }
        */


        /// <summary>
        /// the xml reader
        ///</summary>
        XmlReader XmlReader { get; set; }

        /// <summary>
        /// the xml reader
        ///<parameter>the reader</parameter>
        /// </summary>
        public void SetXmlReader(XmlReader reader) => XmlReader = reader;

        ///<summary>
        ///used only in Debug configuration
        ///<param name="token">the token that should be here</param>
        ///</summary>
        void CheckToken(GeometryToken token) {
            if (!XmlReader.IsStartElement(GeometryGraphWriter.FirstCharToLower(token)) &&
                !XmlReader.IsStartElement(token.ToString())) {
                string positionInfo = GetPositionInfo();
                throw new InvalidDataException(
                    string.Format(CultureInfo.InvariantCulture,
                        "expected {0}, {1}", token, positionInfo));
            }
        }

        string GetPositionInfo() {
#if SILVERLIGHT
            return "";
            //todo, figure out how to find the line number for Silverlight
#else
            if (xmlTextReader != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "line {0} col {1}", xmlTextReader.LineNumber,
                    xmlTextReader.LinePosition);
            }

            return string.Empty;
#endif
        }

        /*
                static void CheckToken(XmlReader xmlR, GeometryToken t) {
                    if (!xmlR.IsStartElement(t.ToString())) {
                        throw new InvalidDataException(String.Format(CultureInfo.InvariantCulture, "{0}", t));
                    }
                }
        */

        ///<summary>
        ///reads the end element
        ///</summary>
        void ReadEndElement() => XmlReader.ReadEndElement();


        ///<summary>
        /// reads a double
        ///</summary>        
        double ReadElementContentAsDouble() => XmlReader.ReadElementContentAsDouble();

        ///<summary>
        ///reads the line?
        ///</summary>
        void XmlRead() => XmlReader.Read();


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        int ReadElementContentAsInt() => XmlReader.ReadElementContentAsInt();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing) {
#if !SILVERLIGHT
            if (disposing)
            {
                xmlTextReader.Close();
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

#endif
