using System;
using System.Windows.Forms;

namespace LiveEditor.Tests;

/// <summary>
/// Editor that tests can click and type into without a person at the keyboard.
/// </summary>
internal sealed class TestEditor : RichTextBox
{
    /// <summary>Raises KeyDown.</summary>
    public void RaiseKeyDown(KeyEventArgs Arguments)
    {
        OnKeyDown(Arguments);
    }

    /// <summary>Raises MouseUp.</summary>
    public void RaiseMouseUp(MouseEventArgs Arguments)
    {
        OnMouseUp(Arguments);
    }

    /// <summary>Raises MouseMove.</summary>
    public void RaiseMouseMove(MouseEventArgs Arguments)
    {
        OnMouseMove(Arguments);
    }

    /// <summary>Raises MouseLeave.</summary>
    public void RaiseMouseLeave()
    {
        OnMouseLeave(EventArgs.Empty);
    }
}
