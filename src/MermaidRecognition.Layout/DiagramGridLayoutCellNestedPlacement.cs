using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans nested-group centring through complete parent-grid reservations.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Centres lone nested groups on direct parent members using whole cross-axis cell translations.</summary>
    /// <param name="Contract">Nested membership and direct parent ownership.</param>
    /// <param name="Placed">Complete diagram sharing one grid.</param>
    /// <param name="Horizontal">Whether the cross axis is logical rows.</param>
    /// <returns>Whether the complete legal assignment was accepted.</returns>
    internal static bool CentreCellNestedGroups(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        foreach (var Parent in Contract.Groups.Where(Group => Group.ChildGroupIds.Count == 1))
        {
            string Child = Parent.ChildGroupIds[0];
            var Members = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, Child)).ToArray();
            var Direct = Placed.Where(Node => GroupOf(Contract, Node.Id) == Parent.Id && !IsInsideGroup(Contract, Node.Id, Child)).ToArray();
            if (Members.Length == 0 || Direct.Length == 0 || Members.Any(Node => Node.PlacementHintLocked)) continue;
            long InnerStart = Members.Min(Node => Horizontal ? Targets[Node.Id].StartRow : Targets[Node.Id].StartColumn);
            long InnerEnd = Members.Max(Node => Horizontal ? Targets[Node.Id].EndRow : Targets[Node.Id].EndColumn);
            long ParentStart = Direct.Min(Node => Horizontal ? Targets[Node.Id].StartRow : Targets[Node.Id].StartColumn);
            long ParentEnd = Direct.Max(Node => Horizontal ? Targets[Node.Id].EndRow : Targets[Node.Id].EndColumn);
            int Shift = checked((int)Math.Floor((ParentStart + ParentEnd - InnerStart - InnerEnd) / 2d));
            foreach (var Node in Members)
            {
                var Region = Targets[Node.Id];
                Targets[Node.Id] = new(Horizontal ? checked(Region.StartRow + Shift) : Region.StartRow,
                    Horizontal ? Region.StartColumn : checked(Region.StartColumn + Shift), Region.RowSpan, Region.ColumnSpan);
            }
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }
}
