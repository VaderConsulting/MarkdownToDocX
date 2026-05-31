using System.Text.RegularExpressions;
using MermaidRecognition.Class;
using MermaidRecognition.Contract;

namespace MermaidRecognition.GitGraph;

/// <summary>
/// Maps classified git graph source statements into <see cref="DiagramContract"/> nodes and edges (L3 G4).
/// </summary>
public static class GitGraphContractMapper
{
    private static readonly Regex CommitIdRegex = new(
        @"\bid\s*:\s*""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommitIdBareRegex = new(
        @"\bid\s*:\s*(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BranchRegex = new(
        @"^\s*branch\s+(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CheckoutRegex = new(
        @"^\s*checkout\s+(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SwitchRegex = new(
        @"^\s*switch\s+(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MergeRegex = new(
        @"^\s*merge\s+(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Populates commit and branch nodes with sequential edges on an L2-classified git graph contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        string? PreviousNodeId = null;
        int CommitIndex = 0;
        int EdgeIndex = 0;

        foreach (SourceStatementContract Statement in Contract.SourceStatements)
        {
            string LineTypeId = Statement.LineTypeId;
            string RawLine = Statement.RawLine;

            if (LineTypeId == "gitGraph.accTitle")
            {
                if (ClassAccessibilityParser.TryParseAccTitle(RawLine, out string Title))
                {
                    Contract.Accessibility.Title = Title;
                }

                continue;
            }

            if (LineTypeId == "gitGraph.accDescr")
            {
                if (ClassAccessibilityParser.TryParseAccDescrInline(RawLine, out string Description))
                {
                    Contract.Accessibility.Description = Description;
                }

                continue;
            }

            if (LineTypeId == "gitGraph.commit")
            {
                if (TryParseMergeTarget(RawLine, out string MergeTarget))
                {
                    AddCommitNode(Contract, "merge-" + MergeTarget, ref PreviousNodeId, ref EdgeIndex);
                    CommitIndex++;
                    continue;
                }

                string NodeId = TryParseCommitId(RawLine, out string ParsedId)
                    ? ParsedId
                    : "commit-" + CommitIndex.ToString();
                CommitIndex++;

                AddCommitNode(Contract, NodeId, ref PreviousNodeId, ref EdgeIndex);
                continue;
            }

            if (LineTypeId == "gitGraph.branch")
            {
                if (TryParseBranchName(RawLine, out string BranchName))
                {
                    NodeContract Node = new NodeContract();
                    Node.Id = BranchName;
                    Node.Label = BranchName;
                    Node.Shape = "branch";
                    Contract.NodesMutable.Add(Node);

                    if (PreviousNodeId is not null)
                    {
                        AddEdge(Contract, PreviousNodeId, BranchName, ref EdgeIndex);
                    }

                    PreviousNodeId = BranchName;
                    continue;
                }

                if (TryParseCheckoutOrSwitchTarget(RawLine, out string TargetBranch))
                {
                    PreviousNodeId = TargetBranch;
                }
            }
        }
    }

    static void AddCommitNode(
        DiagramContract Contract,
        string NodeId,
        ref string? PreviousNodeId,
        ref int EdgeIndex)
    {
        NodeContract Node = new NodeContract();
        Node.Id = NodeId;
        Node.Label = NodeId;
        Node.Shape = "commit";
        Contract.NodesMutable.Add(Node);

        if (PreviousNodeId is not null)
        {
            AddEdge(Contract, PreviousNodeId, NodeId, ref EdgeIndex);
        }

        PreviousNodeId = NodeId;
    }

    static void AddEdge(DiagramContract Contract, string SourceId, string TargetId, ref int EdgeIndex)
    {
        EdgeContract Edge = new EdgeContract();
        Edge.SourceId = SourceId;
        Edge.TargetId = TargetId;
        Edge.CreationIndex = EdgeIndex;
        Edge.Id = "L-" + SourceId + "-" + TargetId + "-" + EdgeIndex.ToString();
        EdgeIndex++;
        Contract.EdgesMutable.Add(Edge);
    }

    static bool TryParseBranchName(string RawLine, out string BranchName)
    {
        BranchName = string.Empty;
        Match Match = BranchRegex.Match(RawLine);
        if (!Match.Success)
        {
            return false;
        }

        BranchName = Match.Groups[1].Value.Trim();
        return BranchName.Length > 0;
    }

    static bool TryParseCheckoutOrSwitchTarget(string RawLine, out string TargetBranch)
    {
        TargetBranch = string.Empty;
        Match CheckoutMatch = CheckoutRegex.Match(RawLine);
        if (CheckoutMatch.Success)
        {
            TargetBranch = CheckoutMatch.Groups[1].Value.Trim();
            return TargetBranch.Length > 0;
        }

        Match SwitchMatch = SwitchRegex.Match(RawLine);
        if (SwitchMatch.Success)
        {
            TargetBranch = SwitchMatch.Groups[1].Value.Trim();
            return TargetBranch.Length > 0;
        }

        return false;
    }

    static bool TryParseMergeTarget(string RawLine, out string MergeTarget)
    {
        MergeTarget = string.Empty;
        Match Match = MergeRegex.Match(RawLine);
        if (!Match.Success)
        {
            return false;
        }

        MergeTarget = Match.Groups[1].Value.Trim();
        return MergeTarget.Length > 0;
    }

    static bool TryParseCommitId(string RawLine, out string CommitId)
    {
        CommitId = string.Empty;
        Match QuotedMatch = CommitIdRegex.Match(RawLine);
        if (QuotedMatch.Success)
        {
            CommitId = QuotedMatch.Groups[1].Value.Trim();
            return CommitId.Length > 0;
        }

        Match BareMatch = CommitIdBareRegex.Match(RawLine);
        if (BareMatch.Success)
        {
            CommitId = BareMatch.Groups[1].Value.Trim();
            return CommitId.Length > 0;
        }

        return false;
    }
}
