using System;
using System.Collections.Generic;

namespace LiveEditor;

/// <summary>
/// Maps a line of the last accepted source to the same declaration in the text now in the editor. Used while the
/// accepted picture is kept after a rejected edit, so later gestures can still target the right line.
/// </summary>
internal static class SourceLineMap
{
    private const int MaximumMiddleLines = 3000;

    /// <summary>Returns the 1-based current line for an accepted line, or null when the correspondence is ambiguous.</summary>
    internal static int? Map(string Accepted, string Current, int AcceptedLine)
    {
        string[] Old = Split(Accepted), New = Split(Current);
        if (AcceptedLine < 1 || AcceptedLine > Old.Length) return null;
        int Prefix = 0;
        while (Prefix < Old.Length && Prefix < New.Length && Old[Prefix] == New[Prefix]) Prefix++;
        int Suffix = 0;
        while (Suffix < Old.Length - Prefix && Suffix < New.Length - Prefix && Old[^(Suffix + 1)] == New[^(Suffix + 1)]) Suffix++;
        int Index = AcceptedLine - 1;
        if (Index < Prefix) return AcceptedLine;
        if (Index >= Old.Length - Suffix) return AcceptedLine + (New.Length - Old.Length);
        int OldCount = Old.Length - Prefix - Suffix, NewCount = New.Length - Prefix - Suffix;
        if (OldCount == NewCount) return AcceptedLine;
        if (OldCount == 0 || NewCount == 0 || OldCount > MaximumMiddleLines || NewCount > MaximumMiddleLines) return null;

        // Anchor on lines kept verbatim; a gap between anchors of equal size is a line-for-line replacement.
        int[,] Length = new int[OldCount + 1, NewCount + 1];
        for (int I = OldCount - 1; I >= 0; I--)
            for (int J = NewCount - 1; J >= 0; J--)
                Length[I, J] = Old[Prefix + I] == New[Prefix + J] ? Length[I + 1, J + 1] + 1 : Math.Max(Length[I + 1, J], Length[I, J + 1]);
        List<(int Old, int New)> Anchors = [(-1, -1)];
        for (int I = 0, J = 0; I < OldCount && J < NewCount;)
        {
            if (Old[Prefix + I] == New[Prefix + J]) { Anchors.Add((I, J)); I++; J++; }
            else if (Length[I + 1, J] >= Length[I, J + 1]) I++;
            else J++;
        }
        Anchors.Add((OldCount, NewCount));
        int Target = Index - Prefix;
        for (int A = 1; A < Anchors.Count; A++)
        {
            if (Target > Anchors[A].Old) continue;
            if (Target == Anchors[A].Old) return Prefix + Anchors[A].New + 1;
            int OldGap = Anchors[A].Old - Anchors[A - 1].Old - 1, NewGap = Anchors[A].New - Anchors[A - 1].New - 1;
            return OldGap == NewGap ? Prefix + Anchors[A - 1].New + (Target - Anchors[A - 1].Old) + 1 : null;
        }
        return null;
    }

    private static string[] Split(string Text) => Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
