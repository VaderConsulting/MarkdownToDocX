using System;
using System.Collections.Generic;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Measures enterprise leaf boxes so flowchart layout uses the same sizes the
/// ArchiMate renderer draws.
/// </summary>
internal static class EnterpriseNodeSizer
{
    private const double FONT_CHAR_WIDTH = 6.6;

    private const double LINE_HEIGHT = 15;

    private const double MIN_WIDTH = 120;

    private const double ELEMENT_HEIGHT = 55;

    private const int MAX_CHARS = 22;

    internal const double JUNCTION_SIZE = 26;

    internal static void MeasureLeaf(EnterpriseLayoutNode Node)
    {
        if (Node.Element == null)
        {
            return;
        }

        if (Node.Element.ElementType == EnterpriseElementType.Junction)
        {
            Node.Width = JUNCTION_SIZE;
            Node.Height = JUNCTION_SIZE;
            return;
        }

        bool Is3d = EnterpriseElementCatalog.Elements[Node.Element.ElementType].BoxKind == EnterpriseBoxKind.Box3d;
        double Width = Math.Max(MIN_WIDTH, MaxLineWidth(Node.LabelLines) + 40) + (Is3d ? 28 : 0);
        double Height = Math.Max(ELEMENT_HEIGHT, (Node.LabelLines.Count * LINE_HEIGHT) + 26) + (Is3d ? 8 : 0);
        Node.Width = Width;
        Node.Height = Height;
    }

    internal static (double Width, double Height) MeasureElement(EnterpriseElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            return (JUNCTION_SIZE, JUNCTION_SIZE);
        }

        List<string> Lines = Wrap(Element.Label);
        bool Is3d = EnterpriseElementCatalog.Elements[Element.ElementType].BoxKind == EnterpriseBoxKind.Box3d;
        double Width = Math.Max(MIN_WIDTH, MaxLineWidth(Lines) + 40) + (Is3d ? 28 : 0);
        double Height = Math.Max(ELEMENT_HEIGHT, (Lines.Count * LINE_HEIGHT) + 26) + (Is3d ? 8 : 0);
        return (Width, Height);
    }

    internal static List<string> Wrap(string Label)
    {
        List<string> Lines = new List<string>();
        if (string.IsNullOrEmpty(Label))
        {
            Lines.Add(string.Empty);
            return Lines;
        }

        string[] Words = Label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string Current = string.Empty;
        foreach (string Word in Words)
        {
            string Candidate = Current.Length == 0 ? Word : Current + " " + Word;
            if (Current.Length > 0 && Candidate.Length > MAX_CHARS)
            {
                Lines.Add(Current);
                Current = Word;
            }
            else
            {
                Current = Candidate;
            }
        }

        if (Current.Length > 0)
        {
            Lines.Add(Current);
        }

        if (Lines.Count == 0)
        {
            Lines.Add(Label);
        }

        return Lines;
    }

    internal static double MaxLineWidth(List<string> Lines)
    {
        double Max = 0;
        foreach (string Line in Lines)
        {
            Max = Math.Max(Max, Line.Length * FONT_CHAR_WIDTH);
        }

        return Max;
    }
}
