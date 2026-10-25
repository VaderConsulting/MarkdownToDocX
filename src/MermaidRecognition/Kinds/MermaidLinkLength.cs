using System;

namespace MermaidRecognition.Kinds;

/// <summary>
/// Mermaid link length from the stroke run in front of the end marker.
/// </summary>
public static class MermaidLinkLength
{
    /// <summary>The longest minimum rank span Mermaid stores on an edge.</summary>
    public const int MAXIMUM = 10;

    /// <summary>
    /// Turns a stroke run into a minimum rank span: one less than the run, at least 1, at most <see cref="MAXIMUM"/>.
    /// </summary>
    /// <param name="StrokeCharacterCount">Characters in the stroke, not counting the end marker.</param>
    /// <returns>The minimum rank span.</returns>
    public static int FromStrokeRun(int StrokeCharacterCount)
    {
        return Clamp(StrokeCharacterCount - 1);
    }

    /// <summary>
    /// Keeps a length inside the Mermaid range of 1 to <see cref="MAXIMUM"/>.
    /// </summary>
    /// <param name="Length">Requested minimum rank span.</param>
    /// <returns>The clamped length.</returns>
    public static int Clamp(int Length)
    {
        if (Length < 1)
        {
            return 1;
        }

        if (Length > MAXIMUM)
        {
            return MAXIMUM;
        }

        return Length;
    }

    /// <summary>
    /// Writes the closing dashes and arrow that round-trip to <paramref name="MinLength"/>.
    /// </summary>
    /// <param name="MinLength">Minimum rank span.</param>
    /// <param name="MinimumDashes">Dashes used when the length is 1. Flowchart arrows use 2 (<c>--&gt;</c>); enterprise relationships use 1 (<c>-&gt;</c>).</param>
    /// <returns>The arrow text, without surrounding spaces.</returns>
    public static string Arrow(int MinLength, int MinimumDashes)
    {
        int Length = Clamp(MinLength);
        int Dashes = Length <= 1 ? Math.Max(1, MinimumDashes) : Length + 1;
        return new string('-', Dashes) + ">";
    }
}
