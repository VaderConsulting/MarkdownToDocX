using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace LiveEditor;

/// <summary>Worker candidates are isolated; only the current UI request can publish them.</summary>
internal sealed partial class RetainedEnterprisePreview
{
    private readonly object Gate = new();
    private readonly string DocumentId = Guid.NewGuid().ToString();
    private DiagramSourceUpdateService? Service;
    private FlowchartSvgCoordinateFrame? Frame;
    private string? PresentationGeometry;
    private string? Paint;
    private EnterpriseLabelSnapshot Labels = EnterpriseLabelSnapshot.Empty;
    private ImmutableDictionary<string, RetainedLabelScene> LabelHistory = ImmutableDictionary<string, RetainedLabelScene>.Empty;
    private RetainedLabelScene? LabelScene;
    private long Revision;
    private long AcceptedRevision;
    private bool Retired;

    internal RetainedEnterprisePreview() { }

    private RetainedEnterprisePreview(RetainedEnterprisePreview Original)
    {
        DocumentId = Original.DocumentId;
        Service = Original.Service!.ForkForEvaluation();
        Frame = new() { MinLeft = Original.Frame!.MinLeft, MaxTop = Original.Frame.MaxTop, DiagramPaddingPx = Original.Frame.DiagramPaddingPx };
        PresentationGeometry = Original.PresentationGeometry; Paint = Original.Paint; Labels = Original.Labels;
        LabelHistory = Original.LabelHistory; LabelScene = Original.LabelScene;
        Revision = Original.Revision; AcceptedRevision = Original.AcceptedRevision;
    }

    internal RetainedEnterprisePreview ForkForEvaluation()
    {
        lock (Gate)
        {
            if (Retired || Service == null || Frame == null) throw new InvalidOperationException("No current retained scene is available for placement preview.");
            return new(this);
        }
    }

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
        DiagramLayoutOptions Options, bool AllowInitialLayout = true)
    {
        DiagramSourceUpdateService? Current;
        FlowchartSvgCoordinateFrame? CurrentFrame;
        string? PreviousPresentation;
        string? PreviousPaint;
        EnterpriseLabelSnapshot PreviousLabels;
        ImmutableDictionary<string, RetainedLabelScene> History;
        RetainedLabelScene? PreviousScene;
        long BaseRevision;
        lock (Gate)
        {
            if (Retired || Revision != SourceRevision) throw new OperationCanceledException();
            Current = Service; CurrentFrame = Frame; PreviousPresentation = PresentationGeometry;
            PreviousPaint = Paint; BaseRevision = AcceptedRevision;
            PreviousLabels = Labels;
            History = LabelHistory; PreviousScene = LabelScene;
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
        if (Rebuild && !AllowInitialLayout)
            throw new InvalidOperationException("Placement preview requires retained geometry: " + (RebuildReason ?? "No accepted session."));
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
        string Svg;
        EnterpriseLabelSnapshot CandidateLabels;
        string SceneKey = RetainedLabelScene.Key(Snapshot, Presentation, CandidateFrame);
        RetainedLabelScene? Known = Rebuild ? null : History.GetValueOrDefault(SceneKey);
        if (Known != null && (Known.Routes.Routes.Count != Snapshot.Layout.EdgeRoutes.Count
            || Snapshot.Layout.EdgeRoutes.Any(Route => !Known.Routes.Routes.TryGetValue(Route.EdgeId, out var Old) || !Old.Matches(Route))))
        {
            Update = Current!.Prepare(DocumentId, SourceRevision, Source, Model, Options, CapturePlacementOrigins: true, RestoreRoutes: Known.Routes);
            if (Update.FailureReason != null) throw new InvalidOperationException(Update.FailureReason);
            Snapshot = CandidateService.CopyCandidateSnapshot(Update);
        }
        HashSet<string> LabelDependencies = new(StringComparer.Ordinal);
        var PreviousIssues = (Known?.Labels ?? PreviousLabels).Issues.ToDictionary(Issue => Issue.Key, StringComparer.Ordinal);
        while (true)
        {
            if (!EnterpriseFlowchartEdgeRouter.TryBuildRetainedContext(Snapshot.Model, Snapshot.Layout, CandidateFrame, out var Geometry))
                throw new InvalidOperationException("The accepted geometry could not be drawn.");
            var Affected = Update?.AffectedRouteIds.ToHashSet(StringComparer.Ordinal) ?? [];
            var Frozen = Rebuild ? null : Known?.Labels.Placements ?? PreviousLabels.Placements.RemoveRange(Affected);
            Svg = EnterpriseSvgRenderer.RenderRetained(Analysis.Document, Geometry, Frozen, out CandidateLabels);
            if (Rebuild) break;
            var ChangedLabels = Update!.ChangedLabelIds.ToHashSet(StringComparer.Ordinal);
            var NewIssues = CandidateLabels.Issues.Where(Issue => (Known == null && ChangedLabels.Contains(Issue.Identity))
                || !PreviousIssues.TryGetValue(Issue.Key, out var Old) || Issue.Severity > Old.Severity + 0.01).ToArray();
            if (NewIssues.Length == 0) break;
            var Present = Snapshot.Model.Edges.Select(Edge => Edge.Id!).ToHashSet(StringComparer.Ordinal);
            string[] Witnesses = NewIssues.SelectMany(Issue => Issue.OtherIsRoute && Issue.OtherIdentity != null
                    ? new[] { Issue.Identity, Issue.OtherIdentity } : new[] { Issue.Identity })
                .Where(Id => Present.Contains(Id) && !Update.RebuiltRouteIds.Contains(Id)).Distinct(StringComparer.Ordinal).ToArray();
            if (Witnesses.Length == 0 || Witnesses.All(LabelDependencies.Contains))
                throw new InvalidOperationException("The retained label update cannot be placed legally: " + NewIssues[0].Message);
            LabelDependencies.UnionWith(Witnesses);
            if (LabelDependencies.Count > FlowchartLayoutRules.RepairChainLines)
                throw new InvalidOperationException("Label dependencies exceed the local route limit.");
            Update = Current!.Prepare(DocumentId, SourceRevision, Source, Model, Options, CapturePlacementOrigins: true,
                AdditionalRouteSeeds: LabelDependencies);
            if (Update.FailureReason != null) throw new InvalidOperationException(Update.FailureReason);
            Snapshot = CandidateService.CopyCandidateSnapshot(Update);
            Known = null;
        }
        int[] RelationshipLines = [.. Analysis.Document.Relationships.Where(Edge => !Edge.HiddenByNesting).Select(Edge => Edge.LineNumber),
            .. Analysis.Document.PlainConnections.Select(Edge => Edge.LineNumber)];
        var Scene = new RetainedLabelScene(RetainedRouteCheckpoint.Capture(Snapshot.Model, Snapshot.Layout,
            Known?.Routes ?? PreviousScene?.Routes), CandidateLabels);
        return new(this, SourceRevision, BaseRevision, Source, Svg, CandidateService, Update, CandidateFrame,
            Presentation, NextPaint, Rebuild, RebuildReason,
            Rebuild ? "InitialLayout" : Update!.Kind == DiagramSourceUpdateKind.MetadataOnly && NextPaint != PreviousPaint
                ? "Redraw" : Update.Kind.ToString(),
            Update?.AffectedRouteIds.ToArray() ?? [], Update?.RebuiltRouteIds.ToArray() ?? [], CandidateLabels, SceneKey, Scene)
        {
            RouteReport = Options.EnableRouteAwareConstruction ? DiagramRouteDiagnostics.Measure(Snapshot.Model, Snapshot.Layout) : null,
            RouteLines = Snapshot.Identities.Bindings.ToImmutableDictionary(Binding => Binding.Id,
                Binding => Binding.ParsedEdgeIndex < RelationshipLines.Length ? RelationshipLines[Binding.ParsedEdgeIndex] : 0, StringComparer.Ordinal)
        };
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
            Labels = Candidate.Labels;
            LabelScene = Candidate.Scene;
            LabelHistory = (Candidate.Rebuild ? ImmutableDictionary<string, RetainedLabelScene>.Empty : LabelHistory)
                .SetItem(Candidate.SceneKey, Candidate.Scene);
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
        string Kind, string[] AffectedRouteIds, string[] RebuiltRouteIds, EnterpriseLabelSnapshot Labels, string SceneKey, RetainedLabelScene Scene)
    {
        internal bool Committed { get; set; }
        internal DiagramRouteReport? RouteReport { get; init; }
        internal ImmutableDictionary<string, int> RouteLines { get; init; } = ImmutableDictionary<string, int>.Empty;
    }
}
