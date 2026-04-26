namespace MermaidSharp.Rendering;

public class SvgFilter
{
    public required string Id { get; init; }
    public required string Content { get; init; }

    public void ToXml(StringBuilder builder)
    {
        builder.Append($"<filter id=\"{Id}\" x=\"-50%\" y=\"-50%\" width=\"200%\" height=\"200%\">");
        builder.Append(Content);
        builder.Append("</filter>");
    }
}