using System.Text;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Render;

/// <summary>
/// Pilot native SVG renderer from <see cref="DiagramContract"/> (Stage 11.C entry point).
/// </summary>
public static class NativeFlowchartSvgRenderer
{
    private const int NODE_WIDTH = 120;
    private const int NODE_HEIGHT = 48;
    private const int HORIZONTAL_GAP = 80;
    private const int VERTICAL_GAP = 60;
    private const int MARGIN = 24;

    /// <summary>
    /// Renders a minimal SVG document from a flowchart contract.
    /// </summary>
    public static string RenderToSvg(DiagramContract Contract)
    {
        if (!NativeRenderCapabilities.CanRender(Contract))
        {
            return string.Empty;
        }

        Dictionary<string, (int X, int Y)> Positions = AssignGridPositions(Contract);
        int MaxX = Positions.Count > 0 ? Positions.Values.Max(P => P.X) : 0;
        int MaxY = Positions.Count > 0 ? Positions.Values.Max(P => P.Y) : 0;
        int Width = MARGIN * 2 + NODE_WIDTH + MaxX * (NODE_WIDTH + HORIZONTAL_GAP);
        int Height = MARGIN * 2 + NODE_HEIGHT + MaxY * (NODE_HEIGHT + VERTICAL_GAP);

        StringBuilder Builder = new StringBuilder();
        Builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        Builder.Append(Width);
        Builder.Append("\" height=\"");
        Builder.Append(Height);
        Builder.Append("\">");

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Positions.TryGetValue(Edge.SourceId, out (int X, int Y) SourcePos)
                || !Positions.TryGetValue(Edge.TargetId, out (int X, int Y) TargetPos))
            {
                continue;
            }

            int X1 = MARGIN + SourcePos.X * (NODE_WIDTH + HORIZONTAL_GAP) + NODE_WIDTH / 2;
            int Y1 = MARGIN + SourcePos.Y * (NODE_HEIGHT + VERTICAL_GAP) + NODE_HEIGHT / 2;
            int X2 = MARGIN + TargetPos.X * (NODE_WIDTH + HORIZONTAL_GAP) + NODE_WIDTH / 2;
            int Y2 = MARGIN + TargetPos.Y * (NODE_HEIGHT + VERTICAL_GAP) + NODE_HEIGHT / 2;

            Builder.Append("<line x1=\"");
            Builder.Append(X1);
            Builder.Append("\" y1=\"");
            Builder.Append(Y1);
            Builder.Append("\" x2=\"");
            Builder.Append(X2);
            Builder.Append("\" y2=\"");
            Builder.Append(Y2);
            Builder.Append("\" stroke=\"black\"/>");
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!Positions.TryGetValue(Node.Id, out (int X, int Y) Pos))
            {
                continue;
            }

            int Left = MARGIN + Pos.X * (NODE_WIDTH + HORIZONTAL_GAP);
            int Top = MARGIN + Pos.Y * (NODE_HEIGHT + VERTICAL_GAP);
            string Label = string.IsNullOrEmpty(Node.Label) ? Node.Id : Node.Label;

            if (IsDecisionShape(Node))
            {
                int CentreX = Left + NODE_WIDTH / 2;
                int CentreY = Top + NODE_HEIGHT / 2;
                Builder.Append("<polygon points=\"");
                Builder.Append(CentreX);
                Builder.Append(',');
                Builder.Append(Top);
                Builder.Append(' ');
                Builder.Append(Left + NODE_WIDTH);
                Builder.Append(',');
                Builder.Append(CentreY);
                Builder.Append(' ');
                Builder.Append(CentreX);
                Builder.Append(',');
                Builder.Append(Top + NODE_HEIGHT);
                Builder.Append(' ');
                Builder.Append(Left);
                Builder.Append(',');
                Builder.Append(CentreY);
                Builder.Append("\" fill=\"white\" stroke=\"black\"/>");
            }
            else
            {
                Builder.Append("<rect x=\"");
                Builder.Append(Left);
                Builder.Append("\" y=\"");
                Builder.Append(Top);
                Builder.Append("\" width=\"");
                Builder.Append(NODE_WIDTH);
                Builder.Append("\" height=\"");
                Builder.Append(NODE_HEIGHT);
                Builder.Append("\" fill=\"white\" stroke=\"black\"/>");
            }

            Builder.Append("<text x=\"");
            Builder.Append(Left + NODE_WIDTH / 2);
            Builder.Append("\" y=\"");
            Builder.Append(Top + NODE_HEIGHT / 2 + 4);
            Builder.Append("\" text-anchor=\"middle\" font-size=\"12\">");
            Builder.Append(EscapeXml(Label));
            Builder.Append("</text>");
        }

        Builder.Append("</svg>");
        return Builder.ToString();
    }

    private static Dictionary<string, (int X, int Y)> AssignGridPositions(DiagramContract Contract)
    {
        Dictionary<string, (int X, int Y)> Positions = new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
        int Column = 0;

        foreach (NodeContract Node in Contract.Nodes)
        {
            Positions[Node.Id] = (Column, 0);
            Column++;
        }

        return Positions;
    }

    private static bool IsDecisionShape(NodeContract Node)
    {
        if (string.IsNullOrEmpty(Node.Shape))
        {
            return false;
        }

        string Normalised = Node.Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeXml(string Value)
    {
        return Value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
