using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// One element-type button: catalogue shape above the type name.
/// </summary>
internal sealed class ElementGlyphButton : Control
{
    internal const int MINIMUM_WIDTH = 76;

    private const int CAPTION_PADDING = 14;

    private const int SHAPE_WIDTH = 32;

    private const int SHAPE_HEIGHT = 20;

    private const int SHAPE_TOP = 2;

    private const int SHAPE_GAP = 4;

    private const int CAPTION_BELOW = 2;

    private const int MAXIMUM_CAPTION_LINES = 2;

    private const int ROW_MARGIN = 3;

    private static readonly TextFormatFlags CAPTION_FLAGS = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    private static readonly ToolTip _TypeTip = CreateTypeTip();

    private EnterpriseElementMetadata? _Metadata;

    private bool _IsLive;

    private int _LineHeight = 16;

    private int _CaptionHeight = 16;

    private int _CaptionTop = 28;

    private bool _IsFitting;

    private static ToolTip CreateTypeTip()
    {
        ToolTip Tip = new ToolTip
        {
            ShowAlways = true,
            InitialDelay = 300,
            AutoPopDelay = 8000
        };
        return Tip;
    }

    /// <summary>
    /// Creates a glyph button.
    /// </summary>
    public ElementGlyphButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Size = new Size(76, 52);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
        FitToFont();
    }

    /// <summary>
    /// Sizes the button and the gap under it from the font that will be drawn.
    /// </summary>
    public void FitToFont()
    {
        if (_IsFitting)
        {
            return;
        }

        _IsFitting = true;
        try
        {
            int Line = CaptionLineHeight();
            _LineHeight = Line;
            _CaptionHeight = MeasureCaptionHeight(Math.Max(1, Width - 2), Line);
            _CaptionTop = SHAPE_TOP + SHAPE_HEIGHT + SHAPE_GAP;
            int NextHeight = _CaptionTop + _CaptionHeight + CAPTION_BELOW;
            if (Height != NextHeight)
            {
                Height = NextHeight;
            }

            Padding NextMargin = new Padding(4, ROW_MARGIN, 4, ROW_MARGIN);
            if (Margin != NextMargin)
            {
                Margin = NextMargin;
            }
        }
        finally
        {
            _IsFitting = false;
        }
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs E)
    {
        base.OnResize(E);
        FitToFont();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs E)
    {
        base.OnHandleCreated(E);
        FitToFont();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs E)
    {
        base.OnFontChanged(E);
        FitToFont();
        Invalidate();
    }

    /// <summary>Gets or sets the element type drawn by this button.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseElementMetadata? Metadata
    {
        get
        {
            return _Metadata;
        }

        set
        {
            _Metadata = value;
            string TypeName = value == null ? string.Empty : ElementShapePainter.DisplayName(value.KebabName);
            AccessibleName = TypeName.Length == 0 ? "Element type" : TypeName;
            _TypeTip.SetToolTip(this, TypeName);
            Invalidate();
        }
    }

    /// <summary>Gets or sets whether this button is the type that matches the current line.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsLive
    {
        get
        {
            return _IsLive;
        }

        set
        {
            if (_IsLive == value)
            {
                return;
            }

            _IsLive = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Width that shows the type name on one line.
    /// </summary>
    public int PreferredWidth()
    {
        string Caption = CaptionText();
        if (Caption.Length == 0)
        {
            return MINIMUM_WIDTH;
        }

        Size Measured = TextRenderer.MeasureText(Caption, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        int Width = Measured.Width + CAPTION_PADDING;
        if (Width < MINIMUM_WIDTH)
        {
            return MINIMUM_WIDTH;
        }

        return Width;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs E)
    {
        E.Graphics.Clear(Parent == null ? SystemColors.Window : Parent.BackColor);
        if (_Metadata != null)
        {
            Rectangle Shape = new Rectangle((Width - SHAPE_WIDTH) / 2, SHAPE_TOP, SHAPE_WIDTH, SHAPE_HEIGHT);
            ElementShapePainter.Draw(E.Graphics, Shape, _Metadata);
        }

        Rectangle Caption = new Rectangle(1, _CaptionTop, Math.Max(1, Width - 2), _CaptionHeight);
        TextRenderer.DrawText(E.Graphics, CaptionText(), Font, Caption, ForeColor, CAPTION_FLAGS);
        if (_IsLive)
        {
            using Pen Border = new Pen(SystemColors.WindowText, 1F);
            E.Graphics.DrawRectangle(Border, 0, 0, Width - 1, Height - 1);
        }
    }

    private string CaptionText()
    {
        if (_Metadata == null)
        {
            return string.Empty;
        }

        return ElementShapePainter.DisplayName(_Metadata.KebabName);
    }

    /// <summary>
    /// Pixel height of one caption line for the font actually in use.
    /// </summary>
    private int CaptionLineHeight()
    {
        int Line = Font.Height;
        if (IsHandleCreated)
        {
            using (Graphics Device = CreateGraphics())
            {
                int DeviceLine = (int)Math.Ceiling(Font.GetHeight(Device));
                if (DeviceLine > Line)
                {
                    Line = DeviceLine;
                }
            }
        }

        Size Ink = TextRenderer.MeasureText("Agjy", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        if (Ink.Height > Line)
        {
            Line = Ink.Height;
        }

        int Slack = Math.Max(2, Line / 8);
        return Line + Slack;
    }

    private int MeasureCaptionHeight(int TextWidth, int Line)
    {
        string Caption = CaptionText();
        if (Caption.Length == 0 || Line <= 0)
        {
            return Line;
        }

        Size Measured = TextRenderer.MeasureText(Caption, Font, new Size(TextWidth, int.MaxValue), CAPTION_FLAGS);
        int Lines = 1;
        if (Measured.Height > Line)
        {
            Lines = (Measured.Height + Line - 1) / Line;
        }

        if (Lines > MAXIMUM_CAPTION_LINES)
        {
            Lines = MAXIMUM_CAPTION_LINES;
        }

        return Lines * Line;
    }
}
