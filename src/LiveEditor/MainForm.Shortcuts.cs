using System;
using System.Windows.Forms;

namespace LiveEditor;

public partial class MainForm
{
    /// <summary>
    /// Edit shortcuts that only exist as labels on the Edit menu rely on a text box having focus to handle them.
    /// After a click or drag in the preview, focus is elsewhere, so Ctrl+Z (and its relatives) did nothing. Text inputs
    /// keep their own undo; everywhere else the shortcut acts on the script, like the matching Edit menu item.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message Msg, Keys KeyData)
    {
        if (TryHandleEditShortcut(KeyData)) return true;
        return base.ProcessCmdKey(ref Msg, KeyData);
    }

    internal bool TryHandleEditShortcut(Keys KeyData)
    {
        Control? Focused = FocusedControl();
        bool InSourceEditor = ReferenceEquals(Focused, CodeView.Editor);
        // Editable text boxes (the source editor, find and replace) keep their native undo and clipboard handling.
        // Read-only ones (details panel, minimap) have nothing to undo, so the shortcut acts on the script.
        bool OwnsTextEditing = Focused is TextBoxBase { ReadOnly: false };
        switch (KeyData)
        {
            case Keys.Control | Keys.Shift | Keys.Z when !OwnsTextEditing || InSourceEditor:
                CodeView.RedoEdit(); return true;
            case Keys.Control | Keys.Z when !OwnsTextEditing:
                CodeView.UndoEdit(); return true;
            case Keys.Control | Keys.Y when !OwnsTextEditing:
                CodeView.RedoEdit(); return true;
            case Keys.Control | Keys.X when !OwnsTextEditing:
                CodeView.CutSelection(); return true;
            case Keys.Control | Keys.C when !OwnsTextEditing:
                CodeView.CopySelection(); return true;
            case Keys.Control | Keys.V when !OwnsTextEditing:
                CodeView.PasteClipboard(); return true;
            default:
                return false;
        }
    }

    private Control? FocusedControl()
    {
        Control? Current = ActiveControl;
        while (Current is ContainerControl Container && Container.ActiveControl != null) Current = Container.ActiveControl;
        return Current;
    }
}
