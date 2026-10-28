using System;
using LiveEditor.Editor;
using MermaidRecognition.Layout;

namespace LiveEditor;

internal static partial class LiveEditorFeatureGate
{
    private static readonly object LayoutSettingsGate = new();

    private static int _DiagramTargetWidthPx = SessionSettings.DEFAULT_DIAGRAM_TARGET_WIDTH_PX;

    public static bool AF_IncrementalDiagramRouting = false;

    public static bool AG_RouteAwareDiagramConstruction = false;

    public static bool AH_WidthConstrainedLayout = false;

    /// <summary>Captures switches once so a background operation uses consistent options.</summary>
    internal static DiagramLayoutOptions CaptureLayoutOptions()
    {
        lock (LayoutSettingsGate)
        {
            return new()
            {
                EnableIncrementalRouting = AF_IncrementalDiagramRouting,
                EnableRouteAwareConstruction = AG_RouteAwareDiagramConstruction,
                EnableWidthConstrainedLayout = AH_WidthConstrainedLayout,
                TargetWidthPx = _DiagramTargetWidthPx
            };
        }
    }

    /// <summary>Applies a saved session atomically before the next preview captures its options.</summary>
    internal static void ApplyLayoutSettings(SessionSettings Settings)
    {
        ArgumentNullException.ThrowIfNull(Settings);
        lock (LayoutSettingsGate)
        {
            AF_IncrementalDiagramRouting = Settings.IncrementalDiagramRouting;
            AG_RouteAwareDiagramConstruction = Settings.RouteAwareDiagramConstruction;
            AH_WidthConstrainedLayout = Settings.WidthConstrainedLayout;
            _DiagramTargetWidthPx = SessionSettings.ClampDiagramTargetWidth(Settings.DiagramTargetWidthPx);
        }
    }
}
