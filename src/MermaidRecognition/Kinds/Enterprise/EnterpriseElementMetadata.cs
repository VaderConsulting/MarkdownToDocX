namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Display and classification facts for one element type.
/// </summary>
public sealed class EnterpriseElementMetadata
{
    private readonly EnterpriseElementType _ElementType;

    private readonly string _KebabName;

    private readonly string _SpecName;

    private readonly EnterpriseLayer _Layer;

    private readonly EnterpriseAspect _Aspect;

    private readonly string? _Icon;

    private readonly EnterpriseBoxKind _BoxKind;

    /// <summary>
    /// Creates element metadata.
    /// </summary>
    /// <param name="ElementType">Enum value.</param>
    /// <param name="KebabName">Kebab-case identifier.</param>
    /// <param name="SpecName">Specification CamelCase name.</param>
    /// <param name="Layer">Application or Technology.</param>
    /// <param name="Aspect">Active, behaviour, or passive.</param>
    /// <param name="Icon">Corner icon key, or null when the box itself identifies the type.</param>
    /// <param name="BoxKind">Outer box style.</param>
    public EnterpriseElementMetadata(
        EnterpriseElementType ElementType,
        string KebabName,
        string SpecName,
        EnterpriseLayer Layer,
        EnterpriseAspect Aspect,
        string? Icon,
        EnterpriseBoxKind BoxKind)
    {
        _ElementType = ElementType;
        _KebabName = KebabName;
        _SpecName = SpecName;
        _Layer = Layer;
        _Aspect = Aspect;
        _Icon = Icon;
        _BoxKind = BoxKind;
    }

    /// <summary>Gets the element type.</summary>
    public EnterpriseElementType ElementType
    {
        get
        {
            return _ElementType;
        }
    }

    /// <summary>Gets the kebab-case identifier.</summary>
    public string KebabName
    {
        get
        {
            return _KebabName;
        }
    }

    /// <summary>Gets the specification CamelCase name.</summary>
    public string SpecName
    {
        get
        {
            return _SpecName;
        }
    }

    /// <summary>Gets the architectural layer.</summary>
    public EnterpriseLayer Layer
    {
        get
        {
            return _Layer;
        }
    }

    /// <summary>Gets the structural aspect.</summary>
    public EnterpriseAspect Aspect
    {
        get
        {
            return _Aspect;
        }
    }

    /// <summary>Gets the corner icon key.</summary>
    public string? Icon
    {
        get
        {
            return _Icon;
        }
    }

    /// <summary>Gets the outer box style.</summary>
    public EnterpriseBoxKind BoxKind
    {
        get
        {
            return _BoxKind;
        }
    }
}
