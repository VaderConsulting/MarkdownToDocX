using System;

namespace LiveEditor.Editor;

internal sealed partial class SessionSettings
{
    public const int DEFAULT_DIAGRAM_TARGET_WIDTH_PX = 1200;
    public const int MINIMUM_DIAGRAM_TARGET_WIDTH_PX = 200;
    public const int MAXIMUM_DIAGRAM_TARGET_WIDTH_PX = 20000;

    public bool IncrementalDiagramRouting { get; set; }

    public bool RouteAwareDiagramConstruction { get; set; }

    public bool WidthConstrainedLayout { get; set; }

    public int DiagramTargetWidthPx { get; set; } = DEFAULT_DIAGRAM_TARGET_WIDTH_PX;

    internal static int ClampDiagramTargetWidth(int Width) => Math.Clamp(Width,
        MINIMUM_DIAGRAM_TARGET_WIDTH_PX, MAXIMUM_DIAGRAM_TARGET_WIDTH_PX);
}
