using System.Collections.Generic;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace LayoutCatalogueBench;

/// <summary>Accumulates top-level layout stage milliseconds during one catalogue render.</summary>
internal sealed class CatalogueLayoutTiming : IDisposable
{
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

        if (FlowchartTopLevelStages.Contains(Stage)
            || Stage is "enterprise-layout" or "enterprise-edge-routing" or "enterprise-analysis")
        {
            LayoutMilliseconds += (long)Math.Round(Milliseconds, MidpointRounding.AwayFromZero);
        }
    }
}
