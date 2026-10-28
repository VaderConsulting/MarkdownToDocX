using System;
using System.Globalization;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

internal static partial class PreviewNodePlacement
{
    internal static bool TryCreateAt(string Source, string NodeId, FlowchartCellRegion Target, out Edit? Edit, out string Reason)
    {
        Edit = null; Reason = "The placement suggestion requires one unpinned element declaration per line.";
        var Analysis = EnterpriseValidator.Analyze(Source);
        if (Analysis.HasErrors || Analysis.Document.Groups.ContainsKey(NodeId)) return false;
        var Before = Details(Analysis.Document, NodeId);
        if (Before == null || Before.Value.Hint?.IsActive == true) return false;
        string Hint = "at " + Target.StartRow.ToString(CultureInfo.InvariantCulture) + " " + Target.StartColumn.ToString(CultureInfo.InvariantCulture);
        if (!TryRewrite(Source, Before.Value.Line, Hint, out Edit)) return false;
        var Parsed = EnterpriseValidator.Analyze(Edit!.Apply(Source));
        var After = Details(Parsed.Document, NodeId);
        if (Parsed.HasErrors || After == null || After.Value.Label != Before.Value.Label || After.Value.Parent != Before.Value.Parent
            || After.Value.Hint is not { Kind: PlacementHintKind.At, IsActive: true } Pin || Pin.CellRow != Target.StartRow || Pin.CellColumn != Target.StartColumn)
        { Edit = null; return false; }
        Reason = ""; return true;
        static (int Line, string Label, string? Parent, PlacementHintContract? Hint)? Details(EnterpriseDocument Document, string Id)
        {
            if (Document.Elements.TryGetValue(Id, out var Element)) return (Element.LineNumber, Element.Label, Element.ParentId, Element.PlacementHint);
            if (Document.Notes.TryGetValue(Id, out var Note)) return (Note.LineNumber, Note.Label, Note.ParentId, Note.PlacementHint);
            return null;
        }
    }
}
