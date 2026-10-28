using System;
using System.Globalization;

namespace MermaidRecognition.Layout;

/// <summary>Immutable experimental layout settings captured for one render or editor operation.</summary>
public sealed record DiagramLayoutOptions
{
    public static DiagramLayoutOptions Default { get; } = new();

    public bool EnableIncrementalRouting { get; init; }

    public bool EnableRouteAwareConstruction { get; init; }

    public bool EnableWidthConstrainedLayout { get; init; }

    private double _TargetWidthPx = 1200;

    /// <summary>Preferred diagram width in model pixels, independent of viewport resizing.</summary>
    public double TargetWidthPx
    {
        get => _TargetWidthPx;
        init
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TargetWidthPx), "Layout width must be finite and positive.");
            }

            _TargetWidthPx = value;
        }
    }

    /// <summary>Separates experimental cache entries; inactive width preferences do not affect geometry.</summary>
    internal string GeometryCacheKey => !EnableIncrementalRouting && !EnableRouteAwareConstruction && !EnableWidthConstrainedLayout
        ? string.Empty
        : "\u001eexperiments|" + EnableIncrementalRouting + "|" + EnableRouteAwareConstruction + "|"
            + EnableWidthConstrainedLayout + "|" + (EnableWidthConstrainedLayout
                ? TargetWidthPx.ToString("R", CultureInfo.InvariantCulture) : string.Empty);
}
