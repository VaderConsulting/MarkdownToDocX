using System;
using System.Linq;
using System.Windows.Forms;

namespace LiveEditor;

internal sealed partial class PreviewCanvas
{
    internal Func<bool>? RelationshipCommandsVisible;
    internal Func<(string Name, Action Execute)[]>? RelationshipAdds;
    internal Func<bool>? DeleteRelationshipAllowed;
    internal event Action? DeleteRelationshipRequested;

    private void RefreshRelationshipMenu()
    {
        bool Visible = RelationshipCommandsVisible?.Invoke() == true;
        AddRelationshipMenuItem.Visible = DeleteRelationshipMenuItem.Visible = Visible;
        foreach (ToolStripItem Item in AddRelationshipMenuItem.DropDownItems.Cast<ToolStripItem>().ToArray()) Item.Dispose();
        AddRelationshipMenuItem.DropDownItems.Clear();
        var Choices = Visible ? RelationshipAdds?.Invoke() ?? [] : [];
        AddRelationshipMenuItem.Enabled = Choices.Length > 0;
        foreach (var Choice in Choices)
        {
            var Item = new ToolStripMenuItem(Choice.Name);
            Item.Click += (_, _) => Choice.Execute();
            AddRelationshipMenuItem.DropDownItems.Add(Item);
        }
        DeleteRelationshipMenuItem.Enabled = Visible && DeleteRelationshipAllowed?.Invoke() == true;
    }

    private void DeleteRelationshipMenuItem_Click(object? Sender, EventArgs E) => RequestRelationshipDelete();
    private void FocusSink_DeletePressed(object? Sender, EventArgs E) => RequestRelationshipDelete();
    private void RequestRelationshipDelete()
    {
        if (CancelNodeMove()) return;
        if (RelationshipCommandsVisible?.Invoke() == true && DeleteRelationshipAllowed?.Invoke() == true)
            DeleteRelationshipRequested?.Invoke();
    }
}
