using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout;

/// <summary>Packs an isolated group's direct members into its best measured row-and-column block.</summary>
internal static class FlowchartGroupMemberPacking
{
    private sealed record Plan(int Columns, int Rows, double Width, double Height, double Score);

    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        if (Contract.Edges.Count != 0) return;
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var ContractNodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var DeclarationOrder = Contract.Nodes.Select((Node, Index) => (Node.Id, Index))
            .GroupBy(Item => Item.Id, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First().Index, StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups.Where(Group => Group.ChildGroupIds.Count == 0))
        {
            List<DiagramGridLayout.GridNode> Members = Group.ChildNodeIds.Where(ById.ContainsKey).Select(Id => ById[Id])
                .OrderBy(Node => DeclarationOrder.GetValueOrDefault(Node.Id, int.MaxValue)).ToList();
            if (Members.Count < 2) continue;
            // A mixed diagram needs the final routed score to judge a group move. Keep this
            // pre-routing pass to standalone file groups, whose complete layout is the block.
            if (Members.Count != Nodes.Count) continue;
            if (Members.Any(Member => !ContractNodes.TryGetValue(Member.Id, out NodeContract? Node)
                || !Node.Metadata.TryGetValue("enterpriseElementType", out MermaidMetadataValue? Metadata)
                || Metadata is not RawMetadata Raw
                || !string.Equals(Raw.RawValue, EnterpriseElementType.Artifact.ToString(), StringComparison.Ordinal))) continue;
            var Inside = Members.Select(Node => Node.Id).Append(Group.Id).ToHashSet(StringComparer.Ordinal);
            bool External = Contract.Edges.Any(Edge =>
                (Inside.Contains(Edge.SourceId) || Inside.Contains(Edge.TargetId))
                && !(Inside.Contains(Edge.SourceId) && Inside.Contains(Edge.TargetId)));
            if (External) continue;
            double Width = Members.Max(Node => Node.Width), Height = Members.Max(Node => Node.Height);
            double ColumnPitch = Width + DiagramGridMetrics.NODE_GAP_PX;
            double RowPitch = Height + DiagramGridMetrics.NODE_GAP_PX;
            Plan Best = Enumerable.Range(1, Members.Count).Select(Columns =>
            {
                int Rows = (Members.Count + Columns - 1) / Columns;
                double BlockWidth = Columns * Width + Math.Max(0, Columns - 1) * DiagramGridMetrics.NODE_GAP_PX;
                double BlockHeight = Rows * Height + Math.Max(0, Rows - 1) * DiagramGridMetrics.NODE_GAP_PX;
                return new Plan(Columns, Rows, BlockWidth, BlockHeight,
                    FlowchartLayoutScore.Calculate(BlockWidth, BlockHeight, 0, 0.0));
            }).OrderBy(Plan => Plan.Score).ThenBy(Plan => Plan.Width * Plan.Height).ThenBy(Plan => Plan.Columns).First();
            double OldLeft = Members.Min(Node => Node.X - Node.Width / 2.0);
            double OldRight = Members.Max(Node => Node.X + Node.Width / 2.0);
            double OldTop = Members.Min(Node => Node.Y - Node.Height / 2.0);
            double OldBottom = Members.Max(Node => Node.Y + Node.Height / 2.0);
            double OldScore = FlowchartLayoutScore.Calculate(OldRight - OldLeft, OldBottom - OldTop, 0, 0.0);
            if (Best.Score >= OldScore - 0.0001) continue;
            double CentreX = (OldLeft + OldRight) / 2.0, CentreY = (OldTop + OldBottom) / 2.0;
            double Left = CentreX - Best.Width / 2.0, Top = CentreY - Best.Height / 2.0;
            var Candidate = Members.Select((Node, Index) => (Node,
                X: Left + (Index % Best.Columns) * ColumnPitch + Width / 2.0,
                Y: Top + (Index / Best.Columns) * RowPitch + Height / 2.0)).ToList();
            var MemberSet = Members.ToHashSet();
            bool Blocked = Candidate.Any(Item => Nodes.Any(Other => !MemberSet.Contains(Other)
                && Math.Abs(Item.X - Other.X) < (Item.Node.Width + Other.Width) / 2.0 + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs(Item.Y - Other.Y) < (Item.Node.Height + Other.Height) / 2.0 + DiagramGridMetrics.NODE_GAP_PX));
            if (Blocked) continue;
            foreach (var Item in Candidate) { Item.Node.X = Item.X; Item.Node.Y = Item.Y; }
        }
    }
}
