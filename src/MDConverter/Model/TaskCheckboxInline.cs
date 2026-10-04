namespace MDConverter.Model;

public sealed class TaskCheckboxInline(bool Checked) : InlineNode
{
    public bool Checked { get; } = Checked;
}
