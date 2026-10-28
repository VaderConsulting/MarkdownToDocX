using System.Collections.Generic;
using System.Drawing;
using MermaidRecognition.Contract;

namespace LiveEditor;

internal static class PreviewEndpointGhost
{
    // Transient screen geometry only: follow an expanded box perimeter so a
    // self-relationship preview cannot cut through its own node.
    internal static PointF[] Loop(RectangleF Box, PointF Start, LayoutHintDirection Leave,
        PointF End, LayoutHintDirection Enter, float Padding = 20)
    {
        Box.Inflate(Padding, Padding);
        int Side(LayoutHintDirection Direction) => Direction switch
            { LayoutHintDirection.North => 0, LayoutHintDirection.East => 1, LayoutHintDirection.South => 2, _ => 3 };
        PointF Outside(PointF Point, int Edge) => Edge switch
            { 0 => new(Point.X, Box.Top), 1 => new(Box.Right, Point.Y), 2 => new(Point.X, Box.Bottom), _ => new(Box.Left, Point.Y) };
        int From = Side(Leave), To = Side(Enter);
        int Clockwise = (To - From + 4) % 4;
        int Step = Clockwise <= 2 ? 1 : -1;
        List<PointF> Path = [Start, Outside(Start, From)];
        for (int SideIndex = From; SideIndex != To; SideIndex = (SideIndex + Step + 4) % 4)
        {
            int Corner = Step > 0 ? SideIndex : (SideIndex + 3) % 4;
            Path.Add(Corner switch { 0 => new(Box.Right, Box.Top), 1 => new(Box.Right, Box.Bottom),
                2 => new(Box.Left, Box.Bottom), _ => new(Box.Left, Box.Top) });
        }
        Path.Add(Outside(End, To)); Path.Add(End);
        return [.. Path];
    }
}
