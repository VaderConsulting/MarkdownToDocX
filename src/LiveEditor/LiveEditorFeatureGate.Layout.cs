using MermaidRecognition.Layout;

namespace LiveEditor;

internal static partial class LiveEditorFeatureGate
{
    public static bool AF_IncrementalDiagramRouting = false;

    public static bool AG_RouteAwareDiagramConstruction = false;

    public static bool AH_WidthConstrainedLayout = false;

    /// <summary>Captures switches once so a background operation uses consistent options.</summary>
    internal static DiagramLayoutOptions CaptureLayoutOptions() => new()
    {
        EnableIncrementalRouting = AF_IncrementalDiagramRouting,
        EnableRouteAwareConstruction = AG_RouteAwareDiagramConstruction,
        EnableWidthConstrainedLayout = AH_WidthConstrainedLayout
    };
}
