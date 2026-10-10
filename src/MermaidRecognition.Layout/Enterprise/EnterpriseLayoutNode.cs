using System.Collections.Generic;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// A laid-out group or element, including nested children.
/// </summary>
internal sealed class EnterpriseLayoutNode
{
    private readonly string _Id;

    private readonly bool _IsGroup;

    private EnterpriseElement? _Element;

    private readonly EnterpriseGroup? _Group;

    private readonly List<string> _LabelLines = new List<string>();

    private readonly List<EnterpriseLayoutNode> _Children = new List<EnterpriseLayoutNode>();

    private double _Width;

    private double _Height;

    private double _RelativeX;

    private double _RelativeY;

    private double _AbsoluteX;

    private double _AbsoluteY;

    internal EnterpriseLayoutNode(string Id, bool IsGroup, EnterpriseElement? Element, EnterpriseGroup? Group)
    {
        _Id = Id;
        _IsGroup = IsGroup;
        _Element = Element;
        _Group = Group;
    }

    internal string Id
    {
        get
        {
            return _Id;
        }
    }

    internal bool IsGroup
    {
        get
        {
            return _IsGroup;
        }
    }

    internal bool IsCompound
    {
        get
        {
            return _Children.Count > 0;
        }
    }

    internal EnterpriseElement? Element
    {
        get
        {
            return _Element;
        }
    }

    /// <summary>
    /// Attaches the typed grouping element to a visual group node so relationships can resolve.
    /// </summary>
    /// <param name="Element">Grouping element with the same id.</param>
    internal void AttachElement(EnterpriseElement Element)
    {
        _Element = Element;
    }

    internal EnterpriseGroup? Group
    {
        get
        {
            return _Group;
        }
    }

    internal List<string> LabelLines
    {
        get
        {
            return _LabelLines;
        }
    }

    internal List<EnterpriseLayoutNode> Children
    {
        get
        {
            return _Children;
        }
    }

    internal double Width
    {
        get
        {
            return _Width;
        }
        set
        {
            _Width = value;
        }
    }

    internal double Height
    {
        get
        {
            return _Height;
        }
        set
        {
            _Height = value;
        }
    }

    internal double RelativeX
    {
        get
        {
            return _RelativeX;
        }
        set
        {
            _RelativeX = value;
        }
    }

    internal double RelativeY
    {
        get
        {
            return _RelativeY;
        }
        set
        {
            _RelativeY = value;
        }
    }

    internal double AbsoluteX
    {
        get
        {
            return _AbsoluteX;
        }
        set
        {
            _AbsoluteX = value;
        }
    }

    internal double AbsoluteY
    {
        get
        {
            return _AbsoluteY;
        }
        set
        {
            _AbsoluteY = value;
        }
    }

    internal string DisplayLabel
    {
        get
        {
            if (_Group != null)
            {
                return _Group.Label;
            }

            if (_Element != null)
            {
                return _Element.Label;
            }

            return _Id;
        }
    }

    internal string? ParentId
    {
        get
        {
            if (_Group != null)
            {
                return _Group.ParentId;
            }

            if (_Element != null)
            {
                return _Element.ParentId;
            }

            return null;
        }
    }
}
