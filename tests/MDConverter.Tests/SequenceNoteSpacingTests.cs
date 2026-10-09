using System.Globalization;
using System.Text.RegularExpressions;
using MermaidSharp;

namespace MDConverter.Tests;

[TestClass]
public sealed class SequenceNoteSpacingTests
{
    const double MessageArrowTipPx = 2.5;
    const double MessageLabelOffsetAboveArrow = 15;
    const double MessageFontSizePx = 16;
    const double MessageSpacing = 35;
    const double InterElementClearancePx = MessageSpacing - MessageLabelOffsetAboveArrow - MessageArrowTipPx;

    [TestMethod]
    [DataRow("Note over Q: A short note")]
    [DataRow("Note left of Q: A longer note that wraps across several lines in its measured box")]
    [DataRow("Note right of Q: Another note")]
    public void Render_SequenceRows_UseEqualClearanceForMessagesAndNotes(string Note)
    {
        string Source = "sequenceDiagram\nparticipant P\nparticipant Q\n"
            + "P->>Q: First\nQ->>P: Second\n" + Note + "\nP->>Q: Third";
        string Svg = Mermaid.Render(Source, RenderOptions.Default);
        double First = GetHorizontalMessageLineY(Svg, 0);
        double Second = GetHorizontalMessageLineY(Svg, 1);
        double Third = GetHorizontalMessageLineY(Svg, 2);
        (double Top, double Bottom) = GetNoteVerticalBounds(Svg);
        double BetweenMessages = Second - MessageLabelOffsetAboveArrow - MessageFontSizePx
            - First - MessageArrowTipPx;
        double AboveNote = Top - Second - MessageArrowTipPx;
        double BelowNote = Third - MessageLabelOffsetAboveArrow - MessageFontSizePx - Bottom;

        Assert.AreEqual(InterElementClearancePx, BetweenMessages, 0.5);
        Assert.AreEqual(BetweenMessages, AboveNote, 0.5);
        Assert.AreEqual(BetweenMessages, BelowNote, 0.5);
    }

    [TestMethod]
    public void Render_NoteAfterSelfMessage_ClearsTheReturningArrow()
    {
        string Svg = Mermaid.Render("""
            sequenceDiagram
            participant P
            participant Q
            P->>P: Internal work
            Note over P: Work completed
            P->>Q: Continue
            """, RenderOptions.Default);
        Match SelfPath = Regex.Match(Svg, @"<path d=""M[\d.]+,(?<y>[\d.]+)\s+C");
        Assert.IsTrue(SelfPath.Success);
        double SelfStart = double.Parse(SelfPath.Groups["y"].Value, CultureInfo.InvariantCulture);
        (double Top, double Bottom) = GetNoteVerticalBounds(Svg);
        double Following = GetHorizontalMessageLineY(Svg, 0);
        Assert.AreEqual(InterElementClearancePx, Top - SelfStart - 20 - MessageArrowTipPx, 0.5);
        Assert.AreEqual(InterElementClearancePx,
            Following - MessageLabelOffsetAboveArrow - MessageFontSizePx - Bottom, 0.5);
    }

    [TestMethod]
    public void Render_SequenceNoteOver_KeepsClearanceFromAdjacentMessages()
    {
        const string Source = """
            sequenceDiagram
            participant P as Producer
            participant Q as Queue
            participant C as Consumer
            P->>Q: Message (expiresAt)
            Note over Q: TTL elapsed
            Q->>C: Skip or route to discard
            """;

        string svg = Mermaid.Render(Source, RenderOptions.Default);

        double firstMessageY = GetHorizontalMessageLineY(svg, 0);
        double secondMessageY = GetHorizontalMessageLineY(svg, 1);
        (double noteTop, double noteBottom) = GetNoteVerticalBounds(svg);

        double clearanceAbove = noteTop - (firstMessageY + MessageArrowTipPx);
        double clearanceBelow = (secondMessageY - MessageLabelOffsetAboveArrow - MessageFontSizePx) - noteBottom;

        Assert.IsGreaterThan(InterElementClearancePx - 0.5, clearanceAbove,
            "Note top should clear the preceding message arrowhead.");
        Assert.IsGreaterThan(InterElementClearancePx - 0.5, clearanceBelow,
            "Note bottom should clear the following message label.");
    }

    static double GetHorizontalMessageLineY(string svg, int index)
    {
        MatchCollection matches = Regex.Matches(
            svg,
            @"<line[^>]+marker-end=""url\(#mermaid-svg_sequence[^""]+\)""[^>]*/>",
            RegexOptions.CultureInvariant);

        var messageYs = new List<double>();
        foreach (Match match in matches)
        {
            double x1 = ReadAttribute(match.Value, "x1");
            double y1 = ReadAttribute(match.Value, "y1");
            double x2 = ReadAttribute(match.Value, "x2");
            double y2 = ReadAttribute(match.Value, "y2");

            if (Math.Abs(x1 - x2) > 1.0 && Math.Abs(y1 - y2) < 0.5)
            {
                messageYs.Add(y1);
            }
        }

        messageYs.Sort();
        Assert.IsGreaterThan(index, messageYs.Count,
            "Expected another sequence message line in the SVG.");
        return messageYs[index];
    }

    static (double Top, double Bottom) GetNoteVerticalBounds(string svg)
    {
        Match match = Regex.Match(
            svg,
            @"<path d=""M[\d.]+,(?<top>[\d.]+)[^""]*""[^>]*fill=""#FFF2B3""",
            RegexOptions.CultureInvariant);

        Assert.IsTrue(match.Success, "Expected a sequence note path in the SVG.");

        double top = double.Parse(match.Groups["top"].Value, CultureInfo.InvariantCulture);
        MatchCollection numbers = Regex.Matches(match.Value, @"[\d.]+", RegexOptions.CultureInvariant);
        var yValues = new List<double>();
        for (int i = 1; i < numbers.Count; i += 2)
        {
            yValues.Add(double.Parse(numbers[i].Value, CultureInfo.InvariantCulture));
        }

        return (top, yValues.Max());
    }

    static double ReadAttribute(string element, string name)
    {
        Match match = Regex.Match(
            element,
            $@"{name}=""([\d.]+)""",
            RegexOptions.CultureInvariant);

        Assert.IsTrue(match.Success, $"Expected attribute '{name}' on SVG element.");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
