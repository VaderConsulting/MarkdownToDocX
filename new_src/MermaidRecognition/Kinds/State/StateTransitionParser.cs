namespace MermaidRecognition.State;

/// <summary>
/// Extracts transition endpoints and optional label from state diagram source lines.
/// </summary>
public static class StateTransitionParser
{
    /// <summary>
    /// Attempts to parse a transition line into source, target, and optional label.
    /// </summary>
    public static bool TryParse(string Line, out StateTransitionParseResult Result)
    {
        Result = new StateTransitionParseResult();
        string Trimmed = Line.Trim();
        int ArrowIndex = Trimmed.IndexOf("-->", StringComparison.Ordinal);
        if (ArrowIndex < 0)
        {
            return false;
        }

        string SourcePart = Trimmed[..ArrowIndex].Trim();
        string TargetPart = Trimmed[(ArrowIndex + 3)..].Trim();
        if (SourcePart.Length == 0 || TargetPart.Length == 0)
        {
            return false;
        }

        string TargetId;
        string? Label = null;
        int ColonIndex = TargetPart.IndexOf(':');
        if (ColonIndex >= 0)
        {
            TargetId = TargetPart[..ColonIndex].Trim();
            string LabelPart = TargetPart[(ColonIndex + 1)..].Trim();
            if (LabelPart.Length > 0)
            {
                Label = LabelPart;
            }
        }
        else
        {
            TargetId = TargetPart;
        }

        if (TargetId.Length == 0)
        {
            return false;
        }

        Result.SourceId = SourcePart;
        Result.TargetId = TargetId;
        Result.Label = Label;
        return true;
    }
}
