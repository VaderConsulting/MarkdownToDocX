using System;
using System.Collections.Generic;
using System.Threading;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves subgraph membership and cluster bounds for layout and validation.
/// </summary>
public static class FlowchartSubgraphMembershipHelper
{
    private sealed record FixedMembership(DiagramContract Contract, Dictionary<string, string?> Parents);

    private static readonly AsyncLocal<FixedMembership?> ActiveMembership = new();

    private sealed class MembershipScope(FixedMembership? Previous) : IDisposable
    {
        public void Dispose() => ActiveMembership.Value = Previous;
    }

    internal static IDisposable ReuseFixedMembership(DiagramContract Contract)
    {
        FixedMembership? Previous = ActiveMembership.Value;
        Dictionary<string, string?> Parents = new(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            Parents.TryAdd(Group.Id, Group.Id);
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            Parents.TryAdd(Node.Id, Node.ParentGroupId);
        }

        ActiveMembership.Value = new(Contract, Parents);
        return new MembershipScope(Previous);
    }

    /// <summary>
    /// Returns true when the node belongs to the group or any nested child group.
    /// </summary>
    public static bool IsNodeMemberOfGroup(
        string NodeId,
        GroupContract Group,
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById)
    {
        foreach (string ChildNodeId in Group.ChildNodeIds)
        {
            if (string.Equals(ChildNodeId, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.Id, NodeId, StringComparison.Ordinal)
                && string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsNodeMemberOfGroup(NodeId, CandidateGroup, Contract, GroupById))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a lookup of group contracts by id.
    /// </summary>
    public static Dictionary<string, GroupContract> BuildGroupById(DiagramContract Contract)
    {
        Dictionary<string, GroupContract> GroupById =
            new Dictionary<string, GroupContract>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupById[Group.Id] = Group;
        }

        return GroupById;
    }

    /// <summary>
    /// Returns the innermost group id containing the node, if any.
    /// </summary>
    public static string? FindInnermostGroupId(string NodeId, DiagramContract Contract)
    {
        if (ActiveMembership.Value is { } Membership && ReferenceEquals(Membership.Contract, Contract))
        {
            return NodeId == null ? null : Membership.Parents.GetValueOrDefault(NodeId);
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, NodeId, StringComparison.Ordinal))
            {
                return Group.Id;
            }
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                return Node.ParentGroupId;
            }
        }

        return null;
    }
}
