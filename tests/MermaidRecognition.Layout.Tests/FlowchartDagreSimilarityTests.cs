using System.Globalization;
using System.Text;
using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Compares dagre placement with mermaid-cli geometry for the flowchart catalogue.
/// </summary>
[TestFixture]
public sealed class FlowchartDagreSimilarityTests
{
    private const double SAME_RANK_TOLERANCE_PX = 1.5;

    private const double SAME_ORDER_TOLERANCE_PX = 1.5;

    /// <summary>
    /// Largest centre gap allowed after one scale and one translation.
    /// </summary>
    private const double CENTRE_OFFSET_TOLERANCE_PX = 8.0;

    [Test]
    public void Catalogue_MatchesMermaidRankOrderClusterAndSides()
    {
        string ReferencePath = FindRepoFile(Path.Combine("DagreReference", "reference.json"));
        string SourcePath = FindRepoFile(Path.Combine("DagreReference", "sources.json"));
        Dictionary<string, string> Sources = ReadSources(SourcePath);
        List<DiagramComparison> Comparisons = new List<DiagramComparison>();
        using (JsonDocument Document = JsonDocument.Parse(File.ReadAllText(ReferencePath)))
        {
            foreach (JsonElement Element in Document.RootElement.EnumerateArray())
            {
                string Id = Element.GetProperty("id").GetString() ?? string.Empty;
                if (!Sources.TryGetValue(Id, out string? Source))
                {
                    continue;
                }

                Comparisons.Add(CompareOne(Id, Source, Element));
            }
        }

        StringBuilder Report = new StringBuilder();
        int RankFailures = 0;
        int OrderFailures = 0;
        int ClusterFailures = 0;
        int SideFailures = 0;
        int CrossingFailures = 0;
        int OffsetFailures = 0;
        foreach (DiagramComparison Comparison in Comparisons)
        {
            if (!Comparison.RankMatch)
            {
                RankFailures++;
            }

            if (!Comparison.OrderMatch)
            {
                OrderFailures++;
            }

            if (!Comparison.ClusterMatch)
            {
                ClusterFailures++;
            }

            if (!Comparison.SideMatch)
            {
                SideFailures++;
            }

            if (!Comparison.CrossingMatch)
            {
                CrossingFailures++;
            }

            if (!Comparison.OffsetMatch)
            {
                OffsetFailures++;
            }

            Report.Append(Comparison.Id);
            Report.Append('\t');
            Report.Append(Comparison.Summary);
            Report.AppendLine();
        }

        string ReportPath = Path.Combine(Path.GetTempPath(), "dagre-similarity.txt");
        File.WriteAllText(ReportPath, Report.ToString());
        TestContext.Out.WriteLine(
            "rank "
            + RankFailures.ToString(CultureInfo.InvariantCulture)
            + " order "
            + OrderFailures.ToString(CultureInfo.InvariantCulture)
            + " cluster "
            + ClusterFailures.ToString(CultureInfo.InvariantCulture)
            + " side "
            + SideFailures.ToString(CultureInfo.InvariantCulture)
            + " crossing "
            + CrossingFailures.ToString(CultureInfo.InvariantCulture)
            + " offset "
            + OffsetFailures.ToString(CultureInfo.InvariantCulture)
            + " of "
            + Comparisons.Count.ToString(CultureInfo.InvariantCulture));
        TestContext.Out.WriteLine(Report.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(RankFailures, Is.EqualTo(0), "rank mismatches");
            Assert.That(OrderFailures, Is.EqualTo(0), "order mismatches");
            Assert.That(ClusterFailures, Is.EqualTo(0), "cluster mismatches");
            Assert.That(SideFailures, Is.EqualTo(0), "entry or exit side mismatches");
            Assert.That(CrossingFailures, Is.EqualTo(0), "crossing count above the reference");
        });
    }

    private static DiagramComparison CompareOne(string Id, string Source, JsonElement Reference)
    {
        DiagramComparison Comparison = new DiagramComparison
        {
            Id = Id,
            RankMatch = true,
            OrderMatch = true,
            ClusterMatch = true,
            SideMatch = true,
            CrossingMatch = true,
            OffsetMatch = true
        };
        DiagramContract Contract = FlowchartRecognizer.Parse(Source);
        if (!FlowchartDagrePlacement.TryPlace(Contract, out FlowchartLayoutResult Layout))
        {
            Comparison.RankMatch = false;
            Comparison.Summary = "placement failed";
            return Comparison;
        }

        bool Horizontal = IsHorizontal(Contract.Direction);
        List<PlacedNode> ReferenceNodes = ReadNodes(Reference);
        List<PlacedNode> OurNodes = new List<PlacedNode>();
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            OurNodes.Add(new PlacedNode
            {
                Id = Bounds.NodeId,
                X = Bounds.CenterX,
                Y = Bounds.CenterY
            });
        }

        List<List<string>> ReferenceRanks = BuildRanks(ReferenceNodes, Horizontal);
        List<List<string>> OurRanks = BuildRanks(OurNodes, Horizontal);
        Comparison.RankMatch = SamePartitions(ReferenceRanks, OurRanks);
        Comparison.OrderMatch = SameOrder(ReferenceNodes, OurNodes, ReferenceRanks, Horizontal);
        Comparison.ClusterMatch = SameClusters(Contract, Reference, ReferenceNodes);
        Comparison.SideMatch = SameSides(Contract, Layout, Reference, ReferenceNodes, out int ReferenceCrossings, out int OurCrossings, out string SideDetail);
        Comparison.CrossingMatch = OurCrossings <= ReferenceCrossings;
        double WorstOffset = WorstCentreOffset(ReferenceNodes, OurNodes);
        Comparison.OffsetMatch = WorstOffset <= CENTRE_OFFSET_TOLERANCE_PX;
        Comparison.Summary =
            "rank="
            + (Comparison.RankMatch ? "ok" : "DIFF")
            + " order="
            + (Comparison.OrderMatch ? "ok" : "DIFF")
            + " cluster="
            + (Comparison.ClusterMatch ? "ok" : "DIFF")
            + " side="
            + (Comparison.SideMatch ? "ok" : "DIFF")
            + " cross="
            + OurCrossings.ToString(CultureInfo.InvariantCulture)
            + "/"
            + ReferenceCrossings.ToString(CultureInfo.InvariantCulture)
            + " offset="
            + WorstOffset.ToString("0.0", CultureInfo.InvariantCulture);
        if (!Comparison.RankMatch)
        {
            Comparison.Summary += " our=" + FormatRanks(OurRanks) + " ref=" + FormatRanks(ReferenceRanks);
        }

        if (!Comparison.SideMatch && !string.IsNullOrEmpty(SideDetail))
        {
            Comparison.Summary += " " + SideDetail;
        }

        if (!Comparison.RankMatch || !Comparison.OrderMatch)
        {
            Comparison.Summary += " refpos=" + FormatPositions(ReferenceNodes) + " ourpos=" + FormatPositions(OurNodes);
        }

        return Comparison;
    }

    private static bool IsHorizontal(string? Direction)
    {
        if (string.IsNullOrWhiteSpace(Direction))
        {
            return false;
        }

        string Normalised = Direction.Trim().ToUpperInvariant();
        return Normalised == "LR" || Normalised == "RL";
    }

    private static List<PlacedNode> ReadNodes(JsonElement Reference)
    {
        List<PlacedNode> Nodes = new List<PlacedNode>();
        foreach (JsonElement Node in Reference.GetProperty("nodes").EnumerateArray())
        {
            Nodes.Add(new PlacedNode
            {
                Id = Node.GetProperty("id").GetString() ?? string.Empty,
                X = Node.GetProperty("x").GetDouble(),
                Y = Node.GetProperty("y").GetDouble(),
                Width = Node.GetProperty("w").GetDouble(),
                Height = Node.GetProperty("h").GetDouble()
            });
        }

        return Nodes;
    }

    private static List<List<string>> BuildRanks(List<PlacedNode> Nodes, bool Horizontal)
    {
        List<PlacedNode> Ordered = new List<PlacedNode>(Nodes);
        Ordered.Sort(delegate (PlacedNode Left, PlacedNode Right)
        {
            double LeftAxis = Horizontal ? Left.X : Left.Y;
            double RightAxis = Horizontal ? Right.X : Right.Y;
            int AxisCompare = LeftAxis.CompareTo(RightAxis);
            if (AxisCompare != 0)
            {
                return AxisCompare;
            }

            double LeftCross = Horizontal ? Left.Y : Left.X;
            double RightCross = Horizontal ? Right.Y : Right.X;
            return LeftCross.CompareTo(RightCross);
        });

        List<List<string>> Ranks = new List<List<string>>();
        List<PlacedNode> Current = new List<PlacedNode>();
        foreach (PlacedNode Node in Ordered)
        {
            if (Current.Count == 0)
            {
                Current.Add(Node);
                continue;
            }

            double CurrentAxis = Horizontal ? Current[0].X : Current[0].Y;
            double NodeAxis = Horizontal ? Node.X : Node.Y;
            if (Math.Abs(NodeAxis - CurrentAxis) <= SAME_RANK_TOLERANCE_PX)
            {
                Current.Add(Node);
            }
            else
            {
                Ranks.Add(IdsInOrder(Current, Horizontal));
                Current = new List<PlacedNode> { Node };
            }
        }

        if (Current.Count > 0)
        {
            Ranks.Add(IdsInOrder(Current, Horizontal));
        }

        return Ranks;
    }

    private static List<string> IdsInOrder(List<PlacedNode> Nodes, bool Horizontal)
    {
        List<PlacedNode> Ordered = new List<PlacedNode>(Nodes);
        Ordered.Sort(delegate (PlacedNode Left, PlacedNode Right)
        {
            double LeftCross = Horizontal ? Left.Y : Left.X;
            double RightCross = Horizontal ? Right.Y : Right.X;
            return LeftCross.CompareTo(RightCross);
        });
        List<string> Ids = new List<string>();
        foreach (PlacedNode Node in Ordered)
        {
            Ids.Add(Node.Id);
        }

        return Ids;
    }

    private static bool SamePartitions(List<List<string>> Left, List<List<string>> Right)
    {
        if (Left.Count != Right.Count)
        {
            return false;
        }

        for (int Index = 0; Index < Left.Count; Index++)
        {
            if (!SameSet(Left[Index], Right[Index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameOrder(
        List<PlacedNode> ReferenceNodes,
        List<PlacedNode> OurNodes,
        List<List<string>> ReferenceRanks,
        bool Horizontal)
    {
        Dictionary<string, PlacedNode> ReferenceById = Index(ReferenceNodes);
        Dictionary<string, PlacedNode> OurById = Index(OurNodes);
        foreach (List<string> Rank in ReferenceRanks)
        {
            for (int Index = 0; Index < Rank.Count; Index++)
            {
                for (int Other = Index + 1; Other < Rank.Count; Other++)
                {
                    if (!ReferenceById.TryGetValue(Rank[Index], out PlacedNode? ReferenceLeft)
                        || !ReferenceById.TryGetValue(Rank[Other], out PlacedNode? ReferenceRight)
                        || !OurById.TryGetValue(Rank[Index], out PlacedNode? OurLeft)
                        || !OurById.TryGetValue(Rank[Other], out PlacedNode? OurRight))
                    {
                        return false;
                    }

                    double ReferenceDelta = Cross(ReferenceRight, Horizontal) - Cross(ReferenceLeft, Horizontal);
                    double OurDelta = Cross(OurRight, Horizontal) - Cross(OurLeft, Horizontal);
                    if (Math.Abs(ReferenceDelta) <= SAME_ORDER_TOLERANCE_PX || Math.Abs(OurDelta) <= SAME_ORDER_TOLERANCE_PX)
                    {
                        continue;
                    }

                    if (Math.Sign(ReferenceDelta) != Math.Sign(OurDelta))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static double Cross(PlacedNode Node, bool Horizontal)
    {
        if (Horizontal)
        {
            return Node.Y;
        }

        return Node.X;
    }

    private static bool SameClusters(DiagramContract Contract, JsonElement Reference, List<PlacedNode> ReferenceNodes)
    {
        Dictionary<string, PlacedNode> ById = Index(ReferenceNodes);
        foreach (GroupContract Group in Contract.Groups)
        {
            JsonElement? Cluster = FindCluster(Reference, Group.Id);
            if (Cluster == null)
            {
                return false;
            }

            double Left = Cluster.Value.GetProperty("x").GetDouble() - (Cluster.Value.GetProperty("w").GetDouble() / 2.0);
            double Right = Cluster.Value.GetProperty("x").GetDouble() + (Cluster.Value.GetProperty("w").GetDouble() / 2.0);
            double Top = Cluster.Value.GetProperty("y").GetDouble() - (Cluster.Value.GetProperty("h").GetDouble() / 2.0);
            double Bottom = Cluster.Value.GetProperty("y").GetDouble() + (Cluster.Value.GetProperty("h").GetDouble() / 2.0);
            HashSet<string> Inside = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlacedNode Node in ReferenceNodes)
            {
                if (Node.X >= Left - 1.0 && Node.X <= Right + 1.0 && Node.Y >= Top - 1.0 && Node.Y <= Bottom + 1.0)
                {
                    Inside.Add(Node.Id);
                }
            }

            HashSet<string> Expected = DescendantNodes(Contract, Group.Id);
            foreach (string NodeId in Expected)
            {
                if (!ById.ContainsKey(NodeId))
                {
                    return false;
                }

                if (!Inside.Contains(NodeId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static HashSet<string> DescendantNodes(DiagramContract Contract, string GroupId)
    {
        HashSet<string> Result = new HashSet<string>(StringComparer.Ordinal);
        GroupContract? Group = null;
        foreach (GroupContract Candidate in Contract.Groups)
        {
            if (Candidate.Id == GroupId)
            {
                Group = Candidate;
                break;
            }
        }

        if (Group == null)
        {
            return Result;
        }

        foreach (string ChildId in Group.ChildNodeIds)
        {
            Result.Add(ChildId);
        }

        foreach (string ChildGroupId in Group.ChildGroupIds)
        {
            foreach (string Nested in DescendantNodes(Contract, ChildGroupId))
            {
                Result.Add(Nested);
            }
        }

        return Result;
    }

    private static JsonElement? FindCluster(JsonElement Reference, string GroupId)
    {
        foreach (JsonElement Cluster in Reference.GetProperty("clusters").EnumerateArray())
        {
            string Id = Cluster.GetProperty("id").GetString() ?? string.Empty;
            if (Id == GroupId || Id == "diagram-" + GroupId)
            {
                return Cluster;
            }
        }

        return null;
    }

    private static bool SameSides(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        JsonElement Reference,
        List<PlacedNode> ReferenceNodes,
        out int ReferenceCrossings,
        out int OurCrossings,
        out string Detail)
    {
        List<Polyline> ReferenceEdges = ReadEdges(Reference, ReferenceNodes);
        ReferenceCrossings = CountCrossings(ReferenceEdges);
        List<Polyline> OurEdges = new List<Polyline>();
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            Polyline Line = new Polyline
            {
                SourceId = Route.SourceId,
                TargetId = Route.TargetId,
                ExitSide = Route.ExitSide,
                EntrySide = Route.EntrySide
            };
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Line.Points.Add(new PlacedNode
                {
                    X = Point.X,
                    Y = Point.Y
                });
            }

            OurEdges.Add(Line);
        }

        OurCrossings = CountCrossings(OurEdges);
        StringBuilder Notes = new StringBuilder();
        foreach (Polyline ReferenceEdge in ReferenceEdges)
        {
            Polyline? Match = null;
            foreach (Polyline Candidate in OurEdges)
            {
                if (Candidate.Used)
                {
                    continue;
                }

                if (Candidate.SourceId == ReferenceEdge.SourceId && Candidate.TargetId == ReferenceEdge.TargetId)
                {
                    Match = Candidate;
                    break;
                }
            }

            if (Match == null)
            {
                Match = FindRetargeted(Contract, OurEdges, ReferenceEdge);
            }

            if (Match == null)
            {
                Notes.Append(" missing ");
                Notes.Append(ReferenceEdge.SourceId);
                Notes.Append("->");
                Notes.Append(ReferenceEdge.TargetId);
                Detail = Notes.ToString();
                return false;
            }

            Match.Used = true;
            if (!SidesCompatible(Match.ExitSide, ReferenceEdge.ExitSide, ReferenceEdge.Points[0], ReferenceEdge.Points[1])
                || !SidesCompatible(Match.EntrySide, ReferenceEdge.EntrySide, ReferenceEdge.Points[ReferenceEdge.Points.Count - 1], ReferenceEdge.Points[ReferenceEdge.Points.Count - 2]))
            {
                Notes.Append(" ");
                Notes.Append(ReferenceEdge.SourceId);
                Notes.Append("->");
                Notes.Append(ReferenceEdge.TargetId);
                Notes.Append(" ours ");
                Notes.Append(Match.ExitSide);
                Notes.Append("/");
                Notes.Append(Match.EntrySide);
                Notes.Append(" ref ");
                Notes.Append(ReferenceEdge.ExitSide);
                Notes.Append("/");
                Notes.Append(ReferenceEdge.EntrySide);
            }
        }

        Detail = Notes.ToString();
        return Notes.Length == 0;
    }

    private static bool SidesCompatible(string Ours, string Reference, PlacedNode AtBoundary, PlacedNode Towards)
    {
        if (string.Equals(Ours, Reference, StringComparison.Ordinal))
        {
            return true;
        }

        double Dx = Math.Abs(Towards.X - AtBoundary.X);
        double Dy = Math.Abs(Towards.Y - AtBoundary.Y);
        double Larger = Math.Max(Dx, Dy);
        double Smaller = Math.Min(Dx, Dy);
        if (Larger < 0.001)
        {
            return true;
        }

        return Smaller / Larger >= 0.8;
    }

    private static List<Polyline> ReadEdges(JsonElement Reference, List<PlacedNode> ReferenceNodes)
    {
        List<Polyline> Edges = new List<Polyline>();
        foreach (JsonElement Edge in Reference.GetProperty("edges").EnumerateArray())
        {
            Polyline Line = new Polyline();
            foreach (JsonElement Point in Edge.GetProperty("points").EnumerateArray())
            {
                Line.Points.Add(new PlacedNode
                {
                    X = Point.GetProperty("x").GetDouble(),
                    Y = Point.GetProperty("y").GetDouble()
                });
            }

            if (Line.Points.Count < 2)
            {
                continue;
            }

            List<PlacedNode> Clusters = ReadClusters(Reference);
            Line.SourceId = NearestEndpoint(ReferenceNodes, Clusters, Line.Points[0]);
            Line.TargetId = NearestEndpoint(ReferenceNodes, Clusters, Line.Points[Line.Points.Count - 1]);
            Line.ExitSide = SideOf(Line.Points[0], Line.Points[1]);
            Line.EntrySide = SideOf(Line.Points[Line.Points.Count - 1], Line.Points[Line.Points.Count - 2]);
            Edges.Add(Line);
        }

        return Edges;
    }

    private static List<PlacedNode> ReadClusters(JsonElement Reference)
    {
        List<PlacedNode> Clusters = new List<PlacedNode>();
        foreach (JsonElement Cluster in Reference.GetProperty("clusters").EnumerateArray())
        {
            string Id = Cluster.GetProperty("id").GetString() ?? string.Empty;
            if (Id.StartsWith("diagram-", StringComparison.Ordinal))
            {
                Id = Id.Substring("diagram-".Length);
            }

            Clusters.Add(new PlacedNode
            {
                Id = Id,
                X = Cluster.GetProperty("x").GetDouble(),
                Y = Cluster.GetProperty("y").GetDouble(),
                Width = Cluster.GetProperty("w").GetDouble(),
                Height = Cluster.GetProperty("h").GetDouble()
            });
        }

        return Clusters;
    }

    private static string NearestEndpoint(List<PlacedNode> Nodes, List<PlacedNode> Clusters, PlacedNode Point)
    {
        string NodeId = Nearest(Nodes, Point);
        PlacedNode? Node = FindNode(Nodes, NodeId);
        if (Node != null && DistanceToBox(Node, Point) <= 2.0)
        {
            return NodeId;
        }

        string BestCluster = string.Empty;
        double BestArea = double.MaxValue;
        foreach (PlacedNode Cluster in Clusters)
        {
            if (DistanceToBox(Cluster, Point) > 2.0)
            {
                continue;
            }

            double Area = Cluster.Width * Cluster.Height;
            if (Area < BestArea)
            {
                BestArea = Area;
                BestCluster = Cluster.Id;
            }
        }

        if (!string.IsNullOrEmpty(BestCluster))
        {
            return BestCluster;
        }

        return NodeId;
    }

    private static PlacedNode? FindNode(List<PlacedNode> Nodes, string Id)
    {
        foreach (PlacedNode Node in Nodes)
        {
            if (Node.Id == Id)
            {
                return Node;
            }
        }

        return null;
    }

    private static string FaceOf(FlowchartLayoutNodeBounds? Bounds, PlacedNode Point, PlacedNode Towards)
    {
        if (Bounds == null)
        {
            return SideOf(Point, Towards);
        }

        PlacedNode Node = new PlacedNode
        {
            X = Bounds.CenterX,
            Y = Bounds.CenterY,
            Width = Bounds.Width,
            Height = Bounds.Height
        };
        return FaceOf(Node, Point, Towards);
    }

    private static string FaceOf(PlacedNode? Node, PlacedNode Point, PlacedNode Towards)
    {
        if (Node == null || Node.Width <= 0.0 || Node.Height <= 0.0)
        {
            return SideOf(Point, Towards);
        }

        double Left = Math.Abs(Point.X - (Node.X - (Node.Width / 2.0)));
        double Right = Math.Abs(Point.X - (Node.X + (Node.Width / 2.0)));
        double Top = Math.Abs(Point.Y - (Node.Y - (Node.Height / 2.0)));
        double Bottom = Math.Abs(Point.Y - (Node.Y + (Node.Height / 2.0)));
        double Best = Left;
        string Side = "left";
        if (Right < Best)
        {
            Best = Right;
            Side = "right";
        }

        if (Top < Best)
        {
            Best = Top;
            Side = "top";
        }

        if (Bottom < Best)
        {
            Side = "bottom";
        }

        return Side;
    }

    private static Polyline? FindRetargeted(DiagramContract Contract, List<Polyline> Edges, Polyline ReferenceEdge)
    {
        foreach (Polyline Candidate in Edges)
        {
            if (Candidate.Used)
            {
                continue;
            }

            if (EndsMatch(Contract, Candidate.SourceId, ReferenceEdge.SourceId)
                && EndsMatch(Contract, Candidate.TargetId, ReferenceEdge.TargetId))
            {
                return Candidate;
            }
        }

        return null;
    }

    private static bool EndsMatch(DiagramContract Contract, string Ours, string Reference)
    {
        if (Ours == Reference)
        {
            return true;
        }

        HashSet<string> ReferenceMembers = DescendantNodes(Contract, Reference);
        if (ReferenceMembers.Contains(Ours))
        {
            return true;
        }

        HashSet<string> OurMembers = DescendantNodes(Contract, Ours);
        return OurMembers.Contains(Reference);
    }

    private static string Nearest(List<PlacedNode> Nodes, PlacedNode Point)
    {
        string Best = string.Empty;
        double BestDistance = double.MaxValue;
        foreach (PlacedNode Node in Nodes)
        {
            double Distance = DistanceToBox(Node, Point);
            if (Distance < BestDistance)
            {
                BestDistance = Distance;
                Best = Node.Id;
            }
        }

        return Best;
    }

    private static double DistanceToBox(PlacedNode Node, PlacedNode Point)
    {
        double HalfWidth = Node.Width / 2.0;
        double HalfHeight = Node.Height / 2.0;
        if (HalfWidth <= 0.0 && HalfHeight <= 0.0)
        {
            double Dx = Node.X - Point.X;
            double Dy = Node.Y - Point.Y;
            return Math.Sqrt((Dx * Dx) + (Dy * Dy));
        }

        double Left = Node.X - HalfWidth;
        double Right = Node.X + HalfWidth;
        double Top = Node.Y - HalfHeight;
        double Bottom = Node.Y + HalfHeight;
        double OutsideX = 0;
        if (Point.X < Left)
        {
            OutsideX = Left - Point.X;
        }
        else if (Point.X > Right)
        {
            OutsideX = Point.X - Right;
        }

        double OutsideY = 0;
        if (Point.Y < Top)
        {
            OutsideY = Top - Point.Y;
        }
        else if (Point.Y > Bottom)
        {
            OutsideY = Point.Y - Bottom;
        }

        return Math.Sqrt((OutsideX * OutsideX) + (OutsideY * OutsideY));
    }

    private static string SideOf(PlacedNode AtBoundary, PlacedNode Towards)
    {
        double Dx = Towards.X - AtBoundary.X;
        double Dy = Towards.Y - AtBoundary.Y;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            if (Dx >= 0)
            {
                return "right";
            }

            return "left";
        }

        if (Dy >= 0)
        {
            return "bottom";
        }

        return "top";
    }

    private static int CountCrossings(List<Polyline> Edges)
    {
        int Count = 0;
        for (int Left = 0; Left < Edges.Count; Left++)
        {
            for (int Right = Left + 1; Right < Edges.Count; Right++)
            {
                if (SegmentsCross(Edges[Left], Edges[Right]))
                {
                    Count++;
                }
            }
        }

        return Count;
    }

    private static bool SegmentsCross(Polyline Left, Polyline Right)
    {
        for (int LeftIndex = 0; LeftIndex < Left.Points.Count - 1; LeftIndex++)
        {
            for (int RightIndex = 0; RightIndex < Right.Points.Count - 1; RightIndex++)
            {
                if (ProperCross(
                    Left.Points[LeftIndex],
                    Left.Points[LeftIndex + 1],
                    Right.Points[RightIndex],
                    Right.Points[RightIndex + 1]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ProperCross(PlacedNode A, PlacedNode B, PlacedNode C, PlacedNode D)
    {
        double Denominator = ((B.X - A.X) * (D.Y - C.Y)) - ((B.Y - A.Y) * (D.X - C.X));
        if (Math.Abs(Denominator) < 0.0001)
        {
            return false;
        }

        double Left = (((C.X - A.X) * (D.Y - C.Y)) - ((C.Y - A.Y) * (D.X - C.X))) / Denominator;
        double Right = (((C.X - A.X) * (B.Y - A.Y)) - ((C.Y - A.Y) * (B.X - A.X))) / Denominator;
        return Left > 0.02 && Left < 0.98 && Right > 0.02 && Right < 0.98;
    }

    private static double WorstCentreOffset(List<PlacedNode> ReferenceNodes, List<PlacedNode> OurNodes)
    {
        Dictionary<string, PlacedNode> Ours = Index(OurNodes);
        List<PlacedNode> Reference = new List<PlacedNode>();
        List<PlacedNode> OursAligned = new List<PlacedNode>();
        foreach (PlacedNode Node in ReferenceNodes)
        {
            if (!Ours.TryGetValue(Node.Id, out PlacedNode? Match))
            {
                return 100000;
            }

            Reference.Add(Node);
            OursAligned.Add(Match);
        }

        if (Reference.Count == 0)
        {
            return 0;
        }

        double ReferenceMeanX = 0;
        double ReferenceMeanY = 0;
        double OurMeanX = 0;
        double OurMeanY = 0;
        foreach (PlacedNode Node in Reference)
        {
            ReferenceMeanX += Node.X;
            ReferenceMeanY += Node.Y;
        }

        foreach (PlacedNode Node in OursAligned)
        {
            OurMeanX += Node.X;
            OurMeanY += Node.Y;
        }

        ReferenceMeanX /= Reference.Count;
        ReferenceMeanY /= Reference.Count;
        OurMeanX /= OursAligned.Count;
        OurMeanY /= OursAligned.Count;
        double ReferenceEnergy = 0;
        double OurEnergy = 0;
        for (int Index = 0; Index < Reference.Count; Index++)
        {
            double ReferenceX = Reference[Index].X - ReferenceMeanX;
            double ReferenceY = Reference[Index].Y - ReferenceMeanY;
            double OurX = OursAligned[Index].X - OurMeanX;
            double OurY = OursAligned[Index].Y - OurMeanY;
            ReferenceEnergy += (ReferenceX * ReferenceX) + (ReferenceY * ReferenceY);
            OurEnergy += (OurX * OurX) + (OurY * OurY);
        }

        double Scale = 1;
        if (OurEnergy > 0.001)
        {
            Scale = Math.Sqrt(ReferenceEnergy / OurEnergy);
        }

        double Worst = 0;
        for (int Index = 0; Index < Reference.Count; Index++)
        {
            double ReferenceX = Reference[Index].X - ReferenceMeanX;
            double ReferenceY = Reference[Index].Y - ReferenceMeanY;
            double OurX = (OursAligned[Index].X - OurMeanX) * Scale;
            double OurY = (OursAligned[Index].Y - OurMeanY) * Scale;
            double Dx = OurX - ReferenceX;
            double Dy = OurY - ReferenceY;
            double Distance = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (Distance > Worst)
            {
                Worst = Distance;
            }
        }

        return Worst;
    }

    private static Dictionary<string, PlacedNode> Index(List<PlacedNode> Nodes)
    {
        Dictionary<string, PlacedNode> Result = new Dictionary<string, PlacedNode>(StringComparer.Ordinal);
        foreach (PlacedNode Node in Nodes)
        {
            Result[Node.Id] = Node;
        }

        return Result;
    }

    private static bool SameSet(List<string> Left, List<string> Right)
    {
        if (Left.Count != Right.Count)
        {
            return false;
        }

        HashSet<string> Values = new HashSet<string>(Left, StringComparer.Ordinal);
        foreach (string Id in Right)
        {
            if (!Values.Contains(Id))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatPositions(List<PlacedNode> Nodes)
    {
        StringBuilder Text = new StringBuilder();
        foreach (PlacedNode Node in Nodes)
        {
            if (Text.Length > 0)
            {
                Text.Append(" ");
            }

            Text.Append(Node.Id);
            Text.Append("@");
            Text.Append(Node.X.ToString("0", CultureInfo.InvariantCulture));
            Text.Append(",");
            Text.Append(Node.Y.ToString("0", CultureInfo.InvariantCulture));
        }

        return Text.ToString();
    }

    private static string FormatRanks(List<List<string>> Ranks)
    {
        StringBuilder Text = new StringBuilder();
        foreach (List<string> Rank in Ranks)
        {
            if (Text.Length > 0)
            {
                Text.Append(" | ");
            }

            Text.Append(string.Join(",", Rank));
        }

        return Text.ToString();
    }

    private static Dictionary<string, string> ReadSources(string Path)
    {
        Dictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal);
        using (JsonDocument Document = JsonDocument.Parse(File.ReadAllText(Path)))
        {
            foreach (JsonElement Element in Document.RootElement.EnumerateArray())
            {
                string Id = Element.GetProperty("id").GetString() ?? string.Empty;
                string Source = Element.GetProperty("source").GetString() ?? string.Empty;
                Sources[Id] = Source;
            }
        }

        return Sources;
    }

    private static string FindRepoFile(string RelativePath)
    {
        DirectoryInfo? Cursor = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (Cursor != null)
        {
            string Candidate = Path.Combine(Cursor.FullName, "tests", "MermaidRecognition.Layout.Tests", RelativePath);
            if (File.Exists(Candidate))
            {
                return Candidate;
            }

            Candidate = Path.Combine(Cursor.FullName, RelativePath);
            if (File.Exists(Candidate))
            {
                return Candidate;
            }

            Cursor = Cursor.Parent;
        }

        throw new FileNotFoundException(RelativePath);
    }

    private sealed class DiagramComparison
    {
        public string Id { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public bool RankMatch { get; set; }

        public bool OrderMatch { get; set; }

        public bool ClusterMatch { get; set; }

        public bool SideMatch { get; set; }

        public bool CrossingMatch { get; set; }

        public bool OffsetMatch { get; set; }
    }

    private sealed class PlacedNode
    {
        public string Id { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    private sealed class Polyline
    {
        public string SourceId { get; set; } = string.Empty;

        public string TargetId { get; set; } = string.Empty;

        public string ExitSide { get; set; } = string.Empty;

        public string EntrySide { get; set; } = string.Empty;

        public bool Used { get; set; }

        public List<PlacedNode> Points { get; } = new List<PlacedNode>();
    }
}
