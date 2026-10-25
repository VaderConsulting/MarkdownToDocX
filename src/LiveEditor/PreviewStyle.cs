using System;
using System.Drawing;

namespace LiveEditor;

/// <summary>
/// Visual properties of a diagram item: line colour, fill, weight and whether the stroke is bold.
/// </summary>
internal sealed class PreviewStyle
{
    private Color _LineColour;

    private Color _FillColour;

    private bool _Bold;

    private float _Width;

    private bool _Custom;

    /// <summary>
    /// Creates a neutral item style: a thin black stroke and no fill.
    /// </summary>
    public PreviewStyle()
        : this(Color.Black, Color.Empty, false, 1F, false)
    {
    }

    private PreviewStyle(Color LineColour, Color FillColour, bool Bold, float Width, bool Custom)
    {
        _LineColour = LineColour;
        _FillColour = FillColour;
        _Bold = Bold;
        _Width = Width;
        _Custom = Custom;
    }

    /// <summary>
    /// Creates the selection style the diagram already draws: its selection colour, a bold stroke of the
    /// selection width, and no fill.
    /// </summary>
    /// <param name="LineColour">The diagram's current selection colour.</param>
    /// <returns>The diagram's selection style.</returns>
    public static PreviewStyle ForCurrentDiagram(Color LineColour)
    {
        return new PreviewStyle(LineColour, Color.Empty, true, PreviewSelection.LINE_WIDTH, false);
    }

    private Action? _Changed;

    /// <summary>Raised after a visual property changes.</summary>
    internal Action? Changed
    {
        get
        {
            return _Changed;
        }

        set
        {
            _Changed = value;
        }
    }

    /// <summary>Gets whether a visual property has been changed from the style's initial value.</summary>
    internal bool IsCustom
    {
        get
        {
            return _Custom;
        }
    }

    /// <summary>Gets or sets the stroke colour.</summary>
    public Color LineColour
    {
        get
        {
            return _LineColour;
        }

        set
        {
            if (SameColour(_LineColour, value))
            {
                return;
            }

            _LineColour = value;
            _Custom = true;
            Changed?.Invoke();
        }
    }

    /// <summary>Gets or sets the fill colour. An empty colour is not painted.</summary>
    public Color FillColour
    {
        get
        {
            return _FillColour;
        }

        set
        {
            if (SameColour(_FillColour, value))
            {
                return;
            }

            _FillColour = value;
            _Custom = true;
            Changed?.Invoke();
        }
    }

    /// <summary>Gets or sets whether the stroke is drawn at least as heavy as the diagram selection.</summary>
    public bool Bold
    {
        get
        {
            return _Bold;
        }

        set
        {
            if (_Bold == value)
            {
                return;
            }

            _Bold = value;
            _Custom = true;
            Changed?.Invoke();
        }
    }

    /// <summary>Gets or sets the stroke width in pixels.</summary>
    public float Width
    {
        get
        {
            return _Width;
        }

        set
        {
            if (_Width == value)
            {
                return;
            }

            _Width = value;
            _Custom = true;
            Changed?.Invoke();
        }
    }

    /// <summary>Gets the pen width used when this style is drawn.</summary>
    internal float DrawnWidth
    {
        get
        {
            float PenWidth = _Width;
            if (PenWidth <= 0F)
            {
                PenWidth = 1F;
            }

            if (_Bold)
            {
                PenWidth = Math.Max(PenWidth, PreviewSelection.LINE_WIDTH);
            }

            return PenWidth;
        }
    }

    private static bool SameColour(Color Left, Color Right)
    {
        return Left.ToArgb() == Right.ToArgb();
    }
}
