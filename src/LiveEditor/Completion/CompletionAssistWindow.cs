using System;
using System.Windows.Forms;

namespace LiveEditor.Completion;

/// <summary>
/// Borderless popup that hosts completion list, signature, and notes without taking focus.
/// </summary>
internal sealed partial class CompletionAssistWindow : Form
{
    private const int WM_MOUSEACTIVATE = 0x0021;

    private const int MA_NOACTIVATE = 3;

    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// Creates the popup window.
    /// </summary>
    public CompletionAssistWindow()
    {
        InitializeComponent();
    }

    /// <summary>Gets the parameter signature box.</summary>
    internal RichTextBox Signature
    {
        get
        {
            return SignatureBox;
        }
    }

    /// <summary>Gets the member list.</summary>
    internal ListBox Members
    {
        get
        {
            return MemberList;
        }
    }

    /// <summary>Raised when a suggestion row is clicked.</summary>
    internal event EventHandler? MemberChosen;

    /// <summary>Raised when the highlighted suggestion changes.</summary>
    internal event EventHandler? MemberHighlighted;

    /// <summary>Raised when the pointer presses the signature or the notes.</summary>
    internal event MouseEventHandler? SurfacePressed;

    /// <summary>Gets the notes box.</summary>
    internal TextBox Detail
    {
        get
        {
            return DetailBox;
        }
    }

    private void MemberList_Click(object? Sender, EventArgs E)
    {
        MemberChosen?.Invoke(this, E);
    }

    private void MemberList_SelectedIndexChanged(object? Sender, EventArgs E)
    {
        MemberHighlighted?.Invoke(this, E);
    }

    private void Surface_MouseDown(object? Sender, MouseEventArgs E)
    {
        SurfacePressed?.Invoke(this, E);
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
}
