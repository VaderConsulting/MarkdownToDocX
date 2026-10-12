namespace MermaidRecognition.Layout;

/// <summary>
/// Named geometry thresholds shared by placement, routing and rendered-output
/// validation. Each value represents one rule-level decision rather than a
/// convenient local literal.
/// </summary>
internal static class FlowchartGeometryPolicy
{
    internal const double GroupInteriorLongPathMinPx = 160.0;
    internal const double LongDiagonalPathMinPx = 180.0;
    internal const double LongDiagonalCrossAxisMinPx = 40.0;
    internal const double ReversalSpurMinPx = 2.0;
    internal const double ReversalSpurMaxPx = 12.0;
    internal const double ExcessDetourMinPx = 200.0;
    internal const double ShortInitialStubMinPx = 1.0;
    internal const double ShortInitialStubMaxPx = 16.0;
    internal const double WideArrivalFaceMinPx = 100.0;
    internal const double WideArrivalCornerClearancePx = 28.0;
    internal const double CompoundPortInsetPx = 15.0;
    internal const double CompoundTitleInsetPx = 40.0;
    internal const double CompoundPortSpacingPx = 15.0;
    internal const double StackedEndpointDistanceSquaredPx = 100.0;
}
