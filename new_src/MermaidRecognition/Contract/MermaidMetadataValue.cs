namespace MermaidRecognition.Contract;

/// <summary>
/// Base type for typed node metadata from <c>@{ ... }</c> blocks.
/// </summary>
public abstract class MermaidMetadataValue
{
}

/// <summary>
/// Shape override from metadata (canonical Mermaid <c>shortName</c>).
/// </summary>
public sealed class ShapeMetadata : MermaidMetadataValue
{
    public ShapeMetadata(string CanonicalShapeId) => _CanonicalShapeId = CanonicalShapeId;

    private readonly string _CanonicalShapeId;

    public string CanonicalShapeId => _CanonicalShapeId;
}

/// <summary>
/// Label text and type from metadata.
/// </summary>
public sealed class LabelMetadata : MermaidMetadataValue
{
    public LabelMetadata(string Text, MermaidLabelType LabelType)
    {
        _Text = Text;
        _LabelType = LabelType;
    }

    private readonly string _Text;

    public string Text => _Text;

    private readonly MermaidLabelType _LabelType;

    public MermaidLabelType LabelType => _LabelType;
}

/// <summary>
/// Edge curve override from metadata.
/// </summary>
public sealed class CurveMetadata : MermaidMetadataValue
{
    public CurveMetadata(string Curve) => _Curve = Curve;

    private readonly string _Curve;

    public string Curve => _Curve;
}

/// <summary>
/// Animation flags from metadata.
/// </summary>
public sealed class AnimationMetadata : MermaidMetadataValue
{
    public AnimationMetadata(bool? Animate, string? Animation)
    {
        _Animate = Animate;
        _Animation = Animation;
    }

    private readonly bool? _Animate;

    public bool? Animate => _Animate;

    private readonly string? _Animation;

    public string? Animation => _Animation;
}

/// <summary>
/// Unparsed metadata entry preserved for diagnostics.
/// </summary>
public sealed class RawMetadata : MermaidMetadataValue
{
    public RawMetadata(string Key, string RawValue)
    {
        _Key = Key;
        _RawValue = RawValue;
    }

    private readonly string _Key;

    public string Key => _Key;

    private readonly string _RawValue;

    public string RawValue => _RawValue;
}
