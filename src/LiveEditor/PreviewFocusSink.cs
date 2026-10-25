using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor;

/// <summary>
/// An invisible control that takes keyboard focus when the preview is clicked, so Esc can clear the selection
/// without the editor receiving keys meant for the preview.
/// </summary>
internal sealed class PreviewFocusSink : Control
{
    /// <summary>
    /// Creates the focus sink, parked just outside its parent's client area.
    /// </summary>
    public PreviewFocusSink()
    {
        SetStyle(ControlStyles.Selectable, true);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        TabStop = false;
        Name = "PreviewFocusSink";
        Size = new Size(1, 1);
        Location = new Point(-20, -20);
    }

    /// <summary>Raised when Esc is pressed while the sink has focus.</summary>
    public event EventHandler? EscapePressed;

    /// <summary>Raised when Up or Down is pressed while the sink has focus.</summary>
    public event EventHandler<KeyEventArgs>? LineStepPressed;

    /// <summary>
    /// Handles a key pressed while the preview has focus.
    /// </summary>
    /// <param name="Key">Key code.</param>
    /// <returns>True when the key was used.</returns>
    internal bool HandleKey(Keys Key)
    {
        if (Key != Keys.Escape)
        {
            return false;
        }

        EscapePressed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys KeyData)
    {
        Keys Code = KeyData & Keys.KeyCode;
        bool Plain = (KeyData & (Keys.Control | Keys.Alt | Keys.Shift)) == Keys.None;
        if (Code == Keys.Escape || (Plain && (Code == Keys.Up || Code == Keys.Down)))
        {
            return true;
        }

        return base.IsInputKey(KeyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs E)
    {
        base.OnKeyDown(E);
        if (E.Handled)
        {
            return;
        }

        bool Plain = !E.Control && !E.Alt && !E.Shift;
        if (Plain && (E.KeyCode == Keys.Up || E.KeyCode == Keys.Down))
        {
            LineStepPressed?.Invoke(this, E);
            E.Handled = true;
            E.SuppressKeyPress = true;
            return;
        }

        if (HandleKey(E.KeyCode))
        {
            E.Handled = true;
            E.SuppressKeyPress = true;
        }
    }
}
