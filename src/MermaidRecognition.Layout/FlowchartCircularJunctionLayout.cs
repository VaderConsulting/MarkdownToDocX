using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Positions circular collectors before independent per-edge port allocation.</summary>
internal static partial class FlowchartCircularJunctionLayout
{
    internal static void AlignFans(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            // Whole-cell candidates are scored after routing; this pass only
            // centres a junction and its chain and has no cell placement role.
            return;
        }
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> Edges = [.. Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        foreach (DiagramGridLayout.GridNode? Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            List<EdgeContract> Outgoing = [.. Edges.Where(Edge => Edge.SourceId == Junction.Id)];
            if (Outgoing.Count < 2 || Outgoing.Count > 3 || Outgoing.Any(Edge => !ById.ContainsKey(Edge.TargetId)))
            {
                continue;
            }

            List<DiagramGridLayout.GridNode> Children = [.. Outgoing.Select(Edge => ById[Edge.TargetId])];
            if (Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Node => Node.GroupId != Junction.GroupId))
            {
                continue;
            }

            double Centre = Children.Count == 3 ? Cross(Children.OrderBy(Cross).ElementAt(1))
                : (Children.Min(Cross) + Children.Max(Cross)) / 2;
            DiagramGridLayout.GridNode Current = Junction;
            HashSet<string> Seen = [];
            while (Seen.Add(Current.Id))
            {
                double Half = (Horizontal ? Current.Height : Current.Width) / 2.0;
                if (Nodes.Any(Node => Node != Current && Node.Rank == Current.Rank
                    && Math.Abs(Cross(Node) - Centre) < Half + ((Horizontal ? Node.Height : Node.Width) / 2.0)
                        + DiagramGridMetrics.NODE_GAP_PX))
                {
                    break;
                }

                if (Horizontal)
                {
                    Current.Y = Centre;
                }
                else
                {
                    Current.X = Centre;
                }

                List<EdgeContract> Incoming = [.. Edges.Where(Edge => Edge.TargetId == Current.Id)];
                if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out DiagramGridLayout.GridNode? Previous)
                    || Previous.Rank >= Current.Rank || Previous.GroupId != Current.GroupId
                    || Edges.Count(Edge => Edge.SourceId == Previous.Id) != 1)
                {
                    break;
                }

                Current = Previous;
            }
        }
        foreach (DiagramGridLayout.GridNode? Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            List<EdgeContract> Incoming = [.. Edges.Where(Edge => Edge.TargetId == Junction.Id)];
            if (Incoming.Count < 2 || Incoming.Count > 3 || Incoming.Any(Edge => !ById.ContainsKey(Edge.SourceId)))
            {
                continue;
            }

            List<DiagramGridLayout.GridNode> Parents = [.. Incoming.Select(Edge => ById[Edge.SourceId])];
            if (Parents.Select(Node => Node.Rank).Distinct().Count() != 1
                || Parents.Any(Node => Node.GroupId != Junction.GroupId))
            {
                continue;
            }

            double Centre = Parents.Count == 3 ? Cross(Parents.OrderBy(Cross).ElementAt(1))
                : (Parents.Min(Cross) + Parents.Max(Cross)) / 2;
            DiagramGridLayout.GridNode Current = Junction;
            HashSet<string> Seen = [];
            while (Seen.Add(Current.Id))
            {
                double Half = (Horizontal ? Current.Height : Current.Width) / 2.0;
                if (Nodes.Any(Node => Node != Current && Node.Rank == Current.Rank
                    && Math.Abs(Cross(Node) - Centre) < Half + ((Horizontal ? Node.Height : Node.Width) / 2.0)
                        + DiagramGridMetrics.NODE_GAP_PX))
                {
                    break;
                }

                if (Horizontal)
                {
                    Current.Y = Centre;
                }
                else
                {
                    Current.X = Centre;
                }

                List<EdgeContract> Outgoing = [.. Edges.Where(Edge => Edge.SourceId == Current.Id)];
                if (Outgoing.Count != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out DiagramGridLayout.GridNode? Next)
                    || Next.Rank <= Current.Rank || Next.GroupId != Current.GroupId
                    || Edges.Count(Edge => Edge.TargetId == Next.Id) != 1)
                {
                    break;
                }

                Current = Next;
            }
        }
    }
}
