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
        return (KeyData & Keys.KeyCode) == Keys.Escape || base.IsInputKey(KeyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs E)
    {
        base.OnKeyDown(E);
        if (!E.Handled && HandleKey(E.KeyCode))
        {
            E.Handled = true;
            E.SuppressKeyPress = true;
        }
    }
}
