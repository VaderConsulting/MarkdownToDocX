using System.Collections.Generic;
using System.Text.Json;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace LayoutCatalogueBench;

/// <summary>Accumulates layout stage milliseconds during one catalogue render.</summary>
internal sealed class CatalogueLayoutTiming : IDisposable
{
    private static readonly JsonSerializerOptions StagesJsonOptions = new JsonSerializerOptions { WriteIndented = true };

    private static readonly HashSet<string> FlowchartTopLevelStages = new(StringComparer.Ordinal)
    {
        "grid-and-measured-geometry",
        "placement-hints-2.21",
        "automatic-node-placement-2.x",
        "group-frame-fitting",
        "relationship-length",
        "route-coordinate-preparation",
        "joint-port-allocation-4.7",
        "line-routing-hints-3.1-3.7",
        "rule-specific-route-construction",
        "coupled-route-repair",
        "model-route-completion-and-rule-checks",
        "final-legality-validation-section-10",
        "final-complete-layout-repair",
        "bend-removing-port-reassignment-4.12",
    };

    private readonly Action<string, double>? _PreviousGrid;
    private readonly Action<string, double>? _PreviousEnterprise;
    private readonly Action<string, double>? _PreviousEnterpriseBridge;
    private readonly Dictionary<string, long> _StageMilliseconds = new Dictionary<string, long>(StringComparer.Ordinal);

    public CatalogueLayoutTiming()
    {
        _PreviousGrid = DiagramGridLayout.LayoutStageTimingObserver;
        _PreviousEnterprise = EnterpriseSvgRenderer.StageTimingObserver;
        _PreviousEnterpriseBridge = EnterpriseRenderBridge.StageTimingObserver;
        DiagramGridLayout.LayoutStageTimingObserver = OnStage;
        EnterpriseSvgRenderer.StageTimingObserver = OnStage;
        EnterpriseRenderBridge.StageTimingObserver = OnStage;
    }

    public long LayoutMilliseconds { get; private set; }

    public IReadOnlyDictionary<string, long> StageMilliseconds
    {
        get
        {
            return _StageMilliseconds;
        }
    }

    public void WriteStagesJson(string Path)
    {
        List<KeyValuePair<string, long>> Ordered = new List<KeyValuePair<string, long>>(_StageMilliseconds);
        Ordered.Sort((Left, Right) => Right.Value.CompareTo(Left.Value));
        Dictionary<string, long> Payload = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> Entry in Ordered)
        {
            Payload[Entry.Key] = Entry.Value;
        }

        Payload["layout-sum"] = LayoutMilliseconds;
        string Json = JsonSerializer.Serialize(Payload, StagesJsonOptions);
        File.WriteAllText(Path, Json, System.Text.Encoding.UTF8);
    }

    public void Dispose()
    {
        DiagramGridLayout.LayoutStageTimingObserver = _PreviousGrid;
        EnterpriseSvgRenderer.StageTimingObserver = _PreviousEnterprise;
        EnterpriseRenderBridge.StageTimingObserver = _PreviousEnterpriseBridge;
    }

    private void OnStage(string Stage, double Milliseconds)
    {
        if (Milliseconds < 0)
        {
            return;
        }

        long Rounded = (long)Math.Round(Milliseconds, MidpointRounding.AwayFromZero);
        if (_StageMilliseconds.TryGetValue(Stage, out long Existing))
        {
            _StageMilliseconds[Stage] = Existing + Rounded;
        }
        else
        {
            _StageMilliseconds[Stage] = Rounded;
        }

        if (FlowchartTopLevelStages.Contains(Stage)
            || Stage is "enterprise-layout" or "enterprise-edge-routing" or "enterprise-analysis"
            or "enterprise-contract-map" or "enterprise-coordinate-frame" or "enterprise-subgraph-rectangles"
            or "enterprise-shared-flowchart-layout" or "enterprise-svg-build" or "enterprise-svg-postprocess")
        {
            LayoutMilliseconds += Rounded;
        }
    }
}
