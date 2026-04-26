namespace MDConverter.Model;

public sealed class TaskCheckboxInline : InlineNode
{
    public bool Checked { get; }

    public TaskCheckboxInline(bool Checked) => this.Checked = Checked;
}
