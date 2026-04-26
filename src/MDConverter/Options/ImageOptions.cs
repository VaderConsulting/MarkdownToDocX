namespace MDConverter.Options;

/// <summary>
/// Options for image handling (scaling and missing-image placeholder).
/// </summary>
public sealed class ImageOptions
{
    private double _MaxWidthCm = 16.0;
    private bool _ScaleDownOnly = true;

    internal bool HasMaxWidthCm { get; private set; }
    internal bool HasScaleDownOnly { get; private set; }

    /// <summary>
    /// Maximum width of images on the page in centimetres; wider images are scaled down.
    /// </summary>
    public double MaxWidthCm
    {
        get { return _MaxWidthCm; }
        set { _MaxWidthCm = value; HasMaxWidthCm = true; }
    }

    /// <summary>
    /// When true, images smaller than MaxWidthCm are not upscaled.
    /// </summary>
    public bool ScaleDownOnly
    {
        get { return _ScaleDownOnly; }
        set { _ScaleDownOnly = value; HasScaleDownOnly = true; }
    }

    /// <summary>
    /// Format string when an image cannot be resolved, e.g. "[Missing image: {AltText}]".
    /// </summary>
    public string MissingImagePlaceholderFormat { get; set; } = "[Missing image: {AltText}]";

    internal void SetResolvedValues(double MaxWidthCm, bool ScaleDownOnly, string MissingImagePlaceholderFormat)
    {
        _MaxWidthCm = MaxWidthCm;
        _ScaleDownOnly = ScaleDownOnly;
        this.MissingImagePlaceholderFormat = MissingImagePlaceholderFormat;
    }
}
