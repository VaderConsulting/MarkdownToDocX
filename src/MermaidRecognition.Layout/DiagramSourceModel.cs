using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

// The source service owns presentation and source facts as well as routing inputs.
// Immutable metadata, declarations, spans and diagnostics can be shared; mutable bags cannot.
internal static class DiagramSourceModel
{
    internal static DiagramContract Copy(DiagramContract Source)
    {
        DiagramContract Copy = DiagramGridLayout.CopyRoutingInputs(Source);
        Copy.RemarkStatus = Source.RemarkStatus;
        Copy.Accessibility.Title = Source.Accessibility.Title;
        Copy.Accessibility.Description = Source.Accessibility.Description;
        foreach (var Pair in Source.Configuration.RawDirectiveValues) { Copy.Configuration.RawDirectiveValuesMutable.Add(Pair.Key, Pair.Value); }
        foreach (var Pair in Source.Configuration.ValueSources) { Copy.Configuration.ValueSourcesMutable.Add(Pair.Key, Pair.Value); }
        for (int Index = 0; Index < Source.Nodes.Count; Index++)
        {
            Copy.Nodes[Index].DeclarationsMutable.AddRange(Source.Nodes[Index].Declarations);
            CopyStyle(Source.Nodes[Index].InlineStyle, Copy.Nodes[Index].InlineStyle);
        }
        for (int Index = 0; Index < Source.Edges.Count; Index++)
        {
            EdgeContract Edge = Source.Edges[Index];
            Copy.Edges[Index].SourceSpan = Edge.SourceSpan;
            if (Edge.ExpansionTrace is { } Trace)
            {
                Copy.Edges[Index].ExpansionTrace = new(Trace.StatementSpan, Trace.LinkOrdinalInStatement,
                    Trace.StartOrdinal, Trace.EndOrdinal, Trace.StartSet.ToArray(), Trace.EndSet.ToArray(), Trace.RawLinkToken);
            }
        }
        Copy.DiagnosticsMutable.AddRange(Source.Diagnostics);
        Copy.InteractionsMutable.AddRange(Source.Interactions);
        foreach (ClassDefContract Class in Source.ClassDefs)
        {
            ClassDefContract Item = new() { ClassId = Class.ClassId };
            CopyStyle(Class.Style, Item.Style);
            Copy.ClassDefsMutable.Add(Item);
        }
        foreach (LinkStyleDirective Link in Source.LinkStyles)
        {
            LinkStyleDirective Item = Link.IsDefault ? LinkStyleDirective.ForDefault(Link.Style, Link.Curve)
                : LinkStyleDirective.ForIndexes(Link.EdgeIndexes.ToArray(), Link.Style, Link.Curve);
            CopyStyle(Link.Style, Item.Style);
            Copy.LinkStylesMutable.Add(Item);
        }
        foreach (SourceStatementContract Statement in Source.SourceStatements)
        {
            Copy.SourceStatementsMutable.Add(new() { LineNumber = Statement.LineNumber,
                LineTypeId = Statement.LineTypeId, RawLine = Statement.RawLine });
        }
        return Copy;
    }

    private static void CopyStyle(RecognitionStyle Source, RecognitionStyle Target)
    {
        Target.Fill = Source.Fill; Target.Stroke = Source.Stroke; Target.StrokeWidth = Source.StrokeWidth;
        Target.StrokeDasharray = Source.StrokeDasharray; Target.FontFamily = Source.FontFamily;
        Target.FontSize = Source.FontSize; Target.FontWeight = Source.FontWeight;
        Target.TextColor = Source.TextColor; Target.Animation = Source.Animation;
    }

    internal static DiagramContract Bind(DiagramContract Parsed, DiagramRelationshipIdentityMap Identities)
    {
        DiagramContract Model = Copy(Parsed);
        foreach (var Binding in Identities.Bindings)
        {
            Model.Edges[Binding.ParsedEdgeIndex].Id = Binding.Id;
            Model.Edges[Binding.ParsedEdgeIndex].IsUserDefinedId = true;
        }
        return Model;
    }
}
