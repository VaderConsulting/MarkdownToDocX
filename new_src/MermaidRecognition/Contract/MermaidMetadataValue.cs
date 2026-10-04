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
public sealed class ShapeMetadata(string CanonicalShapeId) : MermaidMetadataValue
{
    private readonly string _CanonicalShapeId = CanonicalShapeId;

    public string CanonicalShapeId => _CanonicalShapeId;
}

/// <summary>
/// Label text and type from metadata.
/// </summary>
public sealed class LabelMetadata(string Text, MermaidLabelType LabelType) : MermaidMetadataValue
{
    private readonly string _Text = Text;

    public string Text => _Text;

    private readonly MermaidLabelType _LabelType = LabelType;

    public MermaidLabelType LabelType => _LabelType;
}

/// <summary>
/// Edge curve override from metadata.
/// </summary>
public sealed class CurveMetadata(string Curve) : MermaidMetadataValue
{
    private readonly string _Curve = Curve;

    public string Curve => _Curve;
}

/// <summary>
/// Animation flags from metadata.
/// </summary>
public sealed class AnimationMetadata(bool? Animate, string? Animation) : MermaidMetadataValue
{
    private readonly bool? _Animate = Animate;

    public bool? Animate => _Animate;

    private readonly string? _Animation = Animation;

    public string? Animation => _Animation;
}

/// <summary>
/// Unparsed metadata entry preserved for diagnostics.
/// </summary>
public sealed class RawMetadata(string Key, string RawValue) : MermaidMetadataValue
{
    private readonly string _Key = Key;

    public string Key => _Key;

    private readonly string _RawValue = RawValue;

    public string RawValue => _RawValue;
}
