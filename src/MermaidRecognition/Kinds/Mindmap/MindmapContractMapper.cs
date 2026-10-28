using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Diagnostics;

namespace MermaidRecognition.Kinds.Mindmap;

/// <summary>
/// Maps classified mindmap source statements into hierarchical <see cref="DiagramContract"/> nodes (L3 G4).
/// </summary>
public static class MindmapContractMapper
{
    private const int INDENT_WIDTH = 2;
    private const string ORPHAN_NODE_DIAGNOSTIC_CODE = "mindmap.orphan-node";

    /// <summary>
    /// Populates tree nodes with parent links derived from indentation depth.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        Dictionary<int, string> NodeIdByDepth = [];
        int NodeIndex = 0;

        foreach (SourceStatementContract Statement in Contract.SourceStatements)
        {
            if (Statement.LineTypeId != "mindmap.node")
            {
                continue;
            }

            string RawLine = Statement.RawLine;
            int LeadingSpaces = CountLeadingSpaces(RawLine);
            if (LeadingSpaces < INDENT_WIDTH)
            {
                continue;
            }

            int Depth = LeadingSpaces / INDENT_WIDTH;
            string TrimmedLine = RawLine.Trim();
            if (TrimmedLine.Length == 0)
            {
                continue;
            }

            MindmapParsedNodeLine ParsedNode = MindmapNodeLineParser.Parse(TrimmedLine);

            string? ParentId = null;
            if (Depth > 1)
            {
                if (!NodeIdByDepth.TryGetValue(Depth - 1, out string? ResolvedParentId))
                {
                    AddOrphanNodeDiagnostic(Contract, Statement, ParsedNode.Label);
                    continue;
                }

                ParentId = ResolvedParentId;
            }
            else if (NodeIdByDepth.ContainsKey(1))
            {
                AddOrphanNodeDiagnostic(Contract, Statement, ParsedNode.Label);
                continue;
            }

            string NodeId = "node-" + NodeIndex.ToString();
            NodeIndex++;

            NodeContract Node = new NodeContract
            {
                Id = NodeId,
                Label = ParsedNode.Label,
                Shape = ParsedNode.Shape,
                ShapeSource = "mindmap",
                ParentGroupId = ParentId
            };
            Contract.NodesMutable.Add(Node);

            NodeIdByDepth[Depth] = NodeId;
            List<int> DepthKeys = [.. NodeIdByDepth.Keys.OrderBy(Key => Key)];
            foreach (int Key in DepthKeys)
            {
                if (Key > Depth)
                {
                    NodeIdByDepth.Remove(Key);
                }
            }
        }
    }

    static void AddOrphanNodeDiagnostic(
        DiagramContract Contract,
        SourceStatementContract Statement,
        string Label)
    {
        MermaidSourceSpan SourceSpan = new MermaidSourceSpan(
            0,
            0,
            Statement.LineNumber,
            0,
            Statement.LineNumber,
            Statement.RawLine.Length);

        Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
            MermaidDiagnosticSeverity.Error,
            ORPHAN_NODE_DIAGNOSTIC_CODE,
            "There can be only one root. No parent could be found for (\"" + Label + "\")",
            SourceSpan,
            null));
    }

    static int CountLeadingSpaces(string Line)
    {
        int Count = 0;
        foreach (char Character in Line)
        {
            if (Character == ' ')
            {
                Count++;
            }
            else
            {
                break;
            }
        }

        return Count;
    }
}
