using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor;

/// <summary>
/// Small preview note for the node or relationship under the pointer.
/// It does not take focus, and it closes when told to.
/// </summary>
internal partial class PreviewHoverWindow : Form
{
    private const int WM_MOUSEACTIVATE = 0x0021;

    private const int MA_NOACTIVATE = 3;

    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// Creates the preview note.
    /// </summary>
    public PreviewHoverWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the note beside the pointer.
    /// </summary>
    /// <param name="Owner">The live editor window.</param>
    /// <param name="Text">The sentence to show.</param>
    /// <param name="CursorScreen">Pointer position on the screen.</param>
    public void ShowNear(Form Owner, string Text, Point CursorScreen)
    {
        HoverText.Text = Text;
        Size Measured = TextRenderer.MeasureText(Text, HoverText.Font, new Size(460, 0), TextFormatFlags.WordBreak);
        HoverText.Location = new Point(8, 6);
        HoverText.Size = Measured;
        ClientSize = new Size(Measured.Width + 16, Measured.Height + 12);
        Location = PlaceBeside(CursorScreen);
        if (this.Owner != Owner)
        {
            this.Owner = Owner;
        }

        if (!Visible)
        {
            Show(Owner);
        }
    }

    /// <summary>
    /// Closes the note.
    /// </summary>
    public void HideTip()
    {
        if (Visible)
        {
            Hide();
        }
    }

    /// <inheritdoc />
    protected override bool ShowWithoutActivation
    {
        get
        {
            return true;
        }
    }

    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams Value = base.CreateParams;
            Value.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return Value;
        }
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message Message)
    {
        if (Message.Msg == WM_MOUSEACTIVATE)
        {
            Message.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref Message);
    }

    private Point PlaceBeside(Point CursorScreen)
    {
        Rectangle Area = Screen.FromPoint(CursorScreen).WorkingArea;
        int X = CursorScreen.X + 18;
        int Y = CursorScreen.Y + 22;
        if (X + Width > Area.Right)
        {
            X = CursorScreen.X - Width - 12;
        }

        if (Y + Height > Area.Bottom)
        {
            Y = CursorScreen.Y - Height - 12;
        }

        if (X < Area.Left)
        {
            X = Area.Left;
        }

        if (Y < Area.Top)
        {
            Y = Area.Top;
        }

        return new Point(X, Y);
    }
}
