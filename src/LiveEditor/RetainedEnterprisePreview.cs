using System;
using System.Linq;
using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace LiveEditor;

/// <summary>Worker candidates are isolated; only the current UI request can publish them.</summary>
internal sealed class RetainedEnterprisePreview
{
    private readonly object Gate = new();
    private readonly string DocumentId = Guid.NewGuid().ToString();
    private DiagramSourceUpdateService? Service;
    private FlowchartSvgCoordinateFrame? Frame;
    private string? PresentationGeometry;
    private string? Paint;
    private long Revision;
    private long AcceptedRevision;
    private bool Retired;

    internal void Observe(long SourceRevision)
    {
        lock (Gate)
        {
            Revision = SourceRevision;
            Service?.ObserveSourceRevision(SourceRevision);
        }
    }

    internal void Invalidate(string Reason)
    {
        lock (Gate) { Retired = true; Service?.Invalidate(Reason); }
    }

    internal FlowchartLayoutResult? CopyPlacementLayout()
    {
        lock (Gate) { return Retired ? null : Service?.CopySnapshot().Layout; }
    }

    internal DiagramSourceUpdateService.Snapshot? CopyInteractionSnapshot()
    {
        lock (Gate) { return Retired ? null : Service?.CopySnapshot(); }
    }

    internal Prepared Prepare(string Source, long SourceRevision, EnterpriseAnalysisResult Analysis,
        DiagramLayoutOptions Options)
    {
        DiagramSourceUpdateService? Current;
        FlowchartSvgCoordinateFrame? CurrentFrame;
        string? PreviousPresentation;
        string? PreviousPaint;
        long BaseRevision;
        lock (Gate)
        {
            if (Retired || Revision != SourceRevision) throw new OperationCanceledException();
            Current = Service; CurrentFrame = Frame; PreviousPresentation = PresentationGeometry;
            PreviousPaint = Paint; BaseRevision = AcceptedRevision;
        }
        if (Analysis.HasErrors)
            throw new InvalidOperationException(string.Join(" ", Analysis.Errors.Select(Error => Error.Message)));

        using IDisposable Scope = FlowchartRenderContext.Activate(new() { LayoutOptions = Options });
        var Model = EnterpriseFlowchartContractMapper.Build(Analysis.Document);
        AddEnterpriseFacts(Analysis.Document, Model);
        string Presentation = GeometrySignature(Analysis.Document);
        string NextPaint = JsonSerializer.Serialize(Analysis.Document.Styles);
        string? RebuildReason = null;
        DiagramSourceUpdateService.Update? Update = null;
        if (Current != null)
        {
            if (Current.Options != Options) RebuildReason = "Layout options changed.";
            else if (PreviousPresentation != Presentation) RebuildReason = "Enterprise title, configuration or presentation geometry changed.";
            else
            {
                Update = Current.Prepare(DocumentId, SourceRevision, Source, Model, Options, CapturePlacementOrigins: true);
                if (Update.Kind == DiagramSourceUpdateKind.RebuildRequired) RebuildReason = Update.FailureReason;
                else if (Update.FailureReason != null) throw new InvalidOperationException(Update.FailureReason);
            }
        }

        bool Rebuild = Current == null || RebuildReason != null;
        DiagramSourceUpdateService CandidateService;
        DiagramSourceUpdateService.Snapshot Snapshot;
        FlowchartSvgCoordinateFrame CandidateFrame;
        if (Rebuild)
        {
            if (Model.Nodes.Count == 0 && Model.Groups.Count == 0)
            {
                var Empty = new FlowchartLayoutResult { UsesGridRoutes = true, CellNodes = [],
                    RenderContext = new() { UseCellRegions = true, LayoutOptions = Options } };
                CandidateService = new(DocumentId, SourceRevision, Source, Model, Empty);
                Snapshot = CandidateService.CopySnapshot();
                CandidateFrame = new() { DiagramPaddingPx = 20 };
            }
            else
            {
                var Initial = EnterpriseLayoutEngine.Layout(Analysis.Document).RoutingContext
                ?? throw new InvalidOperationException("Enterprise geometry cannot start a retained session.");
                AddEnterpriseFacts(Analysis.Document, Initial.Contract);
                CandidateService = new(DocumentId, SourceRevision, Source, Initial.Contract, Initial.Layout);
                Snapshot = CandidateService.CopySnapshot();
                CandidateFrame = new()
                {
                    MinLeft = Initial.Frame.MinLeft, MaxTop = Initial.Frame.MaxTop,
                    DiagramPaddingPx = Initial.Frame.DiagramPaddingPx
                };
            }
            Update = null;
        }
        else
        {
            CandidateService = Current!;
            Snapshot = CandidateService.CopyCandidateSnapshot(Update!);
            CandidateFrame = CurrentFrame!;
        }
        if (!EnterpriseFlowchartEdgeRouter.TryBuildRetainedContext(Snapshot.Model, Snapshot.Layout, CandidateFrame, out var Geometry))
            throw new InvalidOperationException("The accepted geometry could not be drawn.");

        string Svg = EnterpriseSvgRenderer.RenderRetained(Analysis.Document, Geometry);
        return new(this, SourceRevision, BaseRevision, Source, Svg, CandidateService, Update, CandidateFrame,
            Presentation, NextPaint, Rebuild, RebuildReason,
            Rebuild ? "InitialLayout" : Update!.Kind == DiagramSourceUpdateKind.MetadataOnly && NextPaint != PreviousPaint
                ? "Redraw" : Update.Kind.ToString(),
            Update?.AffectedRouteIds.ToArray() ?? [], Update?.RebuiltRouteIds.ToArray() ?? []);
    }

    internal bool TryCommit(Prepared Candidate)
    {
        lock (Gate)
        {
            if (Retired || Candidate.Owner != this || Candidate.SourceRevision != Revision
                || Candidate.BaseRevision != AcceptedRevision || Candidate.Committed) return false;
            if (Candidate.Update != null && !Candidate.CandidateService.TryCommit(Candidate.Update, out _)) return false;
            if (Candidate.Rebuild) Service?.Invalidate(Candidate.RebuildReason ?? "New initial layout.");
            Service = Candidate.CandidateService; Frame = Candidate.Frame;
            PresentationGeometry = Candidate.Presentation;
            Paint = Candidate.Paint;
            AcceptedRevision++;
            Candidate.Committed = true;
            return true;
        }
    }

    private static void AddEnterpriseFacts(EnterpriseDocument Document, DiagramContract Model)
    {
        foreach (var Node in Model.Nodes)
        {
            if (Document.Elements.TryGetValue(Node.Id, out var Element))
                Node.MetadataMutable["enterprisePresentation"] = new RawMetadata("enterprisePresentation",
                    JsonSerializer.Serialize(new { Element.IsMarkdownLabel, Element.JunctionKind }));
            else if (Document.Notes.TryGetValue(Node.Id, out var Note))
                Node.MetadataMutable["enterprisePresentation"] = new RawMetadata("enterprisePresentation",
                    JsonSerializer.Serialize(new { Note.IsMarkdownLabel }));
        }
        int Index = 0;
        foreach (var Item in Document.Relationships.Select((Edge, Position) => (Edge, Position)).Where(Item => !Item.Edge.HiddenByNesting))
        {
            var Edge = Item.Edge;
            var Mapped = Model.Edges[Index++];
            Mapped.MetadataMutable["enterpriseDrawing"] = new RawMetadata("enterpriseDrawing",
                JsonSerializer.Serialize(new { Edge.IsMarkdownLabel, Edge.AccessKind, Edge.InfluenceStrength, Edge.DirectedAssociation }));
            var Style = new System.Collections.Generic.Dictionary<string, string>(Document.Styles.DefaultLink, StringComparer.OrdinalIgnoreCase);
            if (Document.Styles.Links.TryGetValue(Item.Position, out var Overrides))
                foreach (var Pair in Overrides) Style[Pair.Key] = Pair.Value;
            Mapped.MetadataMutable["enterpriseDrawingStyle"] = new RawMetadata("enterpriseDrawingStyle",
                JsonSerializer.Serialize(Style.OrderBy(Pair => Pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Where(Pair => Pair.Key.ToLowerInvariant() is not ("fill" or "stroke" or "color" or "opacity" or "fill-opacity" or "stroke-opacity"))));
        }
    }

    // The shared flowchart mapper omits these Enterprise drawing facts. Colour-only
    // styles remain redraws; conservative geometry changes request an identified rebuild.
    private static string GeometrySignature(EnterpriseDocument Document)
    {
        static System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> Properties(
            System.Collections.Generic.Dictionary<string, string> Values)
            => Values.OrderBy(Pair => Pair.Key, StringComparer.OrdinalIgnoreCase)
                .Where(Pair => Pair.Key.ToLowerInvariant() is not ("fill" or "stroke" or "color" or "opacity" or "fill-opacity" or "stroke-opacity"));
        return JsonSerializer.Serialize(new
        {
            Document.Version, Document.Title, Document.AccessibilityTitle, Document.AccessibilityDescription,
            Configuration = Document.Configuration.OrderBy(Pair => Pair.Key, StringComparer.OrdinalIgnoreCase),
            Groups = Document.Groups.Values.OrderBy(Node => Node.Id, StringComparer.Ordinal).Select(Node => new { Node.Id, Node.IsMarkdownLabel }),
            Styles = Document.Styles.Elements.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
                .Where(Pair => Properties(Pair.Value).Any())
                .Select(Pair => new { Pair.Key, Values = Properties(Pair.Value) }),
            DefaultLink = Properties(Document.Styles.DefaultLink)
        });
    }

    internal sealed record Prepared(RetainedEnterprisePreview Owner, long SourceRevision, long BaseRevision, string Source, string Svg,
        DiagramSourceUpdateService CandidateService, DiagramSourceUpdateService.Update? Update,
        FlowchartSvgCoordinateFrame Frame, string Presentation, string Paint, bool Rebuild, string? RebuildReason,
        string Kind, string[] AffectedRouteIds, string[] RebuiltRouteIds)
    {
        internal bool Committed { get; set; }
    }
}
