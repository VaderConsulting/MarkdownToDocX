using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Render;

/// <summary>
/// Normalises diagram direction from contract values (enum names or Mermaid tokens TB/LR/etc.).
/// </summary>
public static class FlowchartDirectionNormalizer
{
    /// <summary>
    /// Parses contract <see cref="DiagramContract.Direction"/> to <see cref="FlowchartDirection"/>.
    /// Omitted direction and <c>TD</c> both resolve to top-to-bottom (spec §3.1).
    /// </summary>
    public static FlowchartDirection Normalize(string? Direction)
    {
        if (string.IsNullOrWhiteSpace(Direction))
        {
            return FlowchartDirection.TopToBottom;
        }

        string Token = Direction.Trim();
        if (Enum.TryParse(Token, ignoreCase: true, out FlowchartDirection Parsed))
        {
            return Parsed;
        }

        return Token.ToUpperInvariant() switch
        {
            "TD" or "TB" => FlowchartDirection.TopToBottom,
            "BT" => FlowchartDirection.BottomToTop,
            "LR" => FlowchartDirection.LeftToRight,
            "RL" => FlowchartDirection.RightToLeft,
            _ => FlowchartDirection.TopToBottom
        };
    }
}
