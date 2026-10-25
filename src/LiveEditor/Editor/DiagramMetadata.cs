using System;
using System.Collections.Generic;
using MermaidRecognition.Core;
using MermaidRecognition.Detection;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Editor;

/// <summary>The kind of drawable item a diagram node is.</summary>
internal enum DiagramNodeKind
{
    /// <summary>A declared element.</summary>
    Element,

    /// <summary>A group frame.</summary>
    Group,

    /// <summary>A free-text note.</summary>
    Note,
}

/// <summary>One node declared in the diagram source.</summary>
internal sealed class DiagramNodeInfo
{
    public DiagramNodeInfo(DiagramNodeKind Kind, string Id, string TypeName, string Label, string? ParentId, int LineNumber)
    {
        this.Kind = Kind;
        this.Id = Id;
        this.TypeName = TypeName;
        this.Label = Label;
        this.ParentId = ParentId;
        this.LineNumber = LineNumber;
    }

    /// <summary>Gets whether the node is an element, a group or a note.</summary>
    public DiagramNodeKind Kind { get; }

    /// <summary>Gets the identifier used in the source.</summary>
    public string Id { get; }

    /// <summary>Gets the type as written in the source, such as <c>application-component</c>, or <c>group</c> and <c>note</c>.</summary>
    public string TypeName { get; }

    /// <summary>Gets the displayed label.</summary>
    public string Label { get; }

    /// <summary>Gets the containing group, or null at the top level.</summary>
    public string? ParentId { get; }

    /// <summary>Gets the 1-based source line.</summary>
    public int LineNumber { get; }
}

/// <summary>One relationship declared in the diagram source.</summary>
internal sealed class DiagramRelationshipInfo
{
    public DiagramRelationshipInfo(string SourceId, string TargetId, string KindName, string Label, int LineNumber)
    {
        this.SourceId = SourceId;
        this.TargetId = TargetId;
        this.KindName = KindName;
        this.Label = Label;
        this.LineNumber = LineNumber;
    }

    /// <summary>Gets the source node id.</summary>
    public string SourceId { get; }

    /// <summary>Gets the target node id.</summary>
    public string TargetId { get; }

    /// <summary>Gets the relationship name, such as <c>serving</c>.</summary>
    public string KindName { get; }

    /// <summary>Gets the optional label.</summary>
    public string Label { get; }

    /// <summary>Gets the 1-based source line.</summary>
    public int LineNumber { get; }
}

/// <summary>
/// Information about the diagram in the editor: its type, its nodes and relationships, and how far its render has got.
/// The content is built once from a single parse of the source and does not change. Only the render progress moves.
/// </summary>
internal sealed class DiagramMetadata
{
    /// <summary>Diagram type reported when the source is empty or not recognised.</summary>
    public const string UNKNOWN_TYPE = "unknown";

    private static readonly IReadOnlyList<DiagramNodeInfo> NO_NODES = Array.Empty<DiagramNodeInfo>();

    private static readonly IReadOnlyList<DiagramRelationshipInfo> NO_RELATIONSHIPS = Array.Empty<DiagramRelationshipInfo>();

    private DiagramMetadata(int Generation, string DiagramType)
    {
        this.Generation = Generation;
        this.DiagramType = DiagramType;
        Nodes = NO_NODES;
        Relationships = NO_RELATIONSHIPS;
        RenderStage = "Waiting";
    }

    /// <summary>Gets the render generation this metadata belongs to. It rises with every edit.</summary>
    public int Generation { get; }

    /// <summary>Gets the diagram type, such as <c>enterprise</c> or <c>flowchart</c>.</summary>
    public string DiagramType { get; }

    /// <summary>Gets the rule-table version for enterprise diagrams, otherwise empty.</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Gets the diagram title, or empty.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the layout direction, or empty when the diagram type has none.</summary>
    public string Direction { get; private set; } = string.Empty;

    /// <summary>Gets every node in source order: elements, groups and notes.</summary>
    public IReadOnlyList<DiagramNodeInfo> Nodes { get; private set; }

    /// <summary>Gets every relationship in source order.</summary>
    public IReadOnlyList<DiagramRelationshipInfo> Relationships { get; private set; }

    /// <summary>Gets the total number of nodes.</summary>
    public int NodeCount
    {
        get
        {
            return Nodes.Count;
        }
    }

    /// <summary>Gets the number of relationships.</summary>
    public int RelationshipCount
    {
        get
        {
            return Relationships.Count;
        }
    }

    /// <summary>Gets the number of parse errors.</summary>
    public int ErrorCount { get; private set; }

    /// <summary>Gets the number of parse warnings.</summary>
    public int WarningCount { get; private set; }

    /// <summary>Gets how much of the render is done, from 0 to 100.</summary>
    public int PercentRendered { get; private set; }

    /// <summary>Gets a short name for the render stage, such as <c>Laying out</c> or <c>Done</c>.</summary>
    public string RenderStage { get; private set; }

    /// <summary>Gets whether the last render attempt failed.</summary>
    public bool RenderFailed { get; private set; }

    /// <summary>Raised on the UI thread when the render progress changes.</summary>
    public event EventHandler? ProgressChanged;

    /// <summary>Metadata for an empty editor.</summary>
    public static DiagramMetadata Empty(int Generation)
    {
        return new DiagramMetadata(Generation, UNKNOWN_TYPE);
    }

    /// <summary>
    /// Builds the metadata from one parse of the source. Safe to call off the UI thread.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <param name="Generation">Render generation the text belongs to.</param>
    public static DiagramMetadata Build(string? Source, int Generation)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return Empty(Generation);
        }

        if (EnterpriseParser.IsEnterpriseSource(Source))
        {
            return BuildEnterprise(Source, Generation);
        }

        return new DiagramMetadata(Generation, DetectType(Source));
    }

    /// <summary>Records render progress. Call on the UI thread.</summary>
    /// <param name="Percent">Percentage done, clamped to 0 to 100.</param>
    /// <param name="Stage">Short stage name.</param>
    public void ReportProgress(int Percent, string Stage)
    {
        PercentRendered = Math.Max(0, Math.Min(100, Percent));
        RenderStage = Stage ?? string.Empty;
        RenderFailed = false;
        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records that the render failed. Call on the UI thread.</summary>
    /// <param name="Stage">Short description of the failure.</param>
    public void ReportFailure(string Stage)
    {
        RenderStage = Stage ?? string.Empty;
        RenderFailed = true;
        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DiagramMetadata BuildEnterprise(string Source, int Generation)
    {
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Source);
        EnterpriseDocument Document = Parsed.Document;
        DiagramMetadata Metadata = new DiagramMetadata(Generation, "enterprise")
        {
            Version = Document.Version,
            Title = Document.Title,
            Direction = Document.Direction.ToString().ToUpperInvariant()
        };

        List<DiagramNodeInfo> Nodes = new List<DiagramNodeInfo>();
        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            Nodes.Add(new DiagramNodeInfo(DiagramNodeKind.Element, Element.Id, EnterpriseElementCatalog.GetKebabName(Element.ElementType), Element.Label, Element.ParentId, Element.LineNumber));
        }

        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            Nodes.Add(new DiagramNodeInfo(DiagramNodeKind.Group, Group.Id, "group", Group.Label, Group.ParentId, Group.LineNumber));
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            Nodes.Add(new DiagramNodeInfo(DiagramNodeKind.Note, Note.Id, "note", Note.Label, Note.ParentId, Note.LineNumber));
        }

        Nodes.Sort(CompareBySourceLine);
        Metadata.Nodes = Nodes;

        List<DiagramRelationshipInfo> Relationships = new List<DiagramRelationshipInfo>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            Relationships.Add(new DiagramRelationshipInfo(Relationship.SourceId, Relationship.TargetId, EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind), Relationship.Label, Relationship.LineNumber));
        }

        Metadata.Relationships = Relationships;
        foreach (EnterpriseDiagnostic Diagnostic in Parsed.Diagnostics)
        {
            if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
            {
                Metadata.ErrorCount++;
            }
            else if (Diagnostic.Severity == MermaidDiagnosticSeverity.Warning)
            {
                Metadata.WarningCount++;
            }
        }

        return Metadata;
    }

    private static int CompareBySourceLine(DiagramNodeInfo Left, DiagramNodeInfo Right)
    {
        return Left.LineNumber.CompareTo(Right.LineNumber);
    }

    private static string DetectType(string Source)
    {
        try
        {
            string DetectionText = RecognitionSourcePreprocessor.PrepareForDetection(Source);
            DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(DetectionText, null);
            if (Detection != null && !string.IsNullOrWhiteSpace(Detection.ProgrammeKindId))
            {
                return Detection.ProgrammeKindId;
            }
        }
        catch (Exception)
        {
            // An unrecognised or half-typed diagram is normal while editing, so the type is simply unknown.
        }

        return UNKNOWN_TYPE;
    }
}