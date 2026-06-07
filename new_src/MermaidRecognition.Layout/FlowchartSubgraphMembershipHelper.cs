using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves subgraph membership and cluster bounds for layout and validation.
/// </summary>
public static class FlowchartSubgraphMembershipHelper
{
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
