using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LiveEditor;

internal sealed partial class PreviewCanvas
{
    internal Func<PreviewObject, PreviewRelationshipMove?>? CreateRelationshipMove;
    internal event Action<PreviewRelationshipMove, bool, PreviewRelationshipMove.Target>? EndpointMoveRequested;
    private PreviewObject? _EndpointItem;
    private PreviewRelationshipMove? _EndpointContext;
    private PreviewRelationshipMove? _MovingRelationship;
    private bool _MovingSource;
    private Point _EndpointStart;
    private Point _EndpointPointer;
    private bool _EndpointThresholdPassed;
    private PreviewRelationshipMove.Target? _EndpointTarget;
    internal bool IsMovingEndpoint => _MovingRelationship != null;

    private PreviewRelationshipMove? SelectedRelationshipMove()
    {
        var Selected = _SelectedItems.Count == 1 ? _SelectedItems[0] : null;
        if (Selected?.Kind != PreviewObjectKind.Relationship) return null;
        if (_EndpointItem != Selected || _EndpointContext == null)
        {
            _EndpointItem = Selected; _EndpointContext = CreateRelationshipMove?.Invoke(Selected);
        }
        return _EndpointContext;
    }

    private bool TryBeginEndpointMove(Point Pointer, bool Modified)
    {
        if (Modified || SelectedRelationshipMove() is not { } Move
            || !TryGetPictureTransform(out double Sx, out double Sy, out double Ox, out double Oy)) return false;
        foreach (bool SourceEnd in new[] { true, false })
        {
            PointF End = Move.Original(SourceEnd).Point;
            if (Math.Pow(Pointer.X - (End.X * Sx + Ox), 2) + Math.Pow(Pointer.Y - (End.Y * Sy + Oy), 2) > 100) continue;
            CancelNodeMove();
            _MovingRelationship = Move; _MovingSource = SourceEnd;
            _EndpointStart = _EndpointPointer = Pointer;
            DiagramPicture.Capture = true; InvalidateNodeMove(); return true;
        }
        return false;
    }

    private void UpdateEndpointMove(Point Pointer)
    {
        if (_MovingRelationship == null) return;
        _EndpointPointer = Pointer;
        Size Threshold = SystemInformation.DragSize;
        _EndpointThresholdPassed |= Math.Abs(Pointer.X - _EndpointStart.X) > Threshold.Width / 2
            || Math.Abs(Pointer.Y - _EndpointStart.Y) > Threshold.Height / 2;
        _EndpointTarget = null;
        if (TryGetPictureTransform(out double Sx, out double Sy, out double Ox, out double Oy))
        {
            double Distance(PreviewRelationshipMove.Target Point) => Math.Pow(Point.Point.X * Sx + Ox - Pointer.X, 2)
                + Math.Pow(Point.Point.Y * Sy + Oy - Pointer.Y, 2);
            var Nearest = _MovingRelationship.Targets(_MovingSource).MinBy(Distance);
            if (Nearest != null && Distance(Nearest) <= 14 * 14) _EndpointTarget = Nearest;
        }
        DiagramPicture.Cursor = _EndpointTarget == null ? Cursors.No : Cursors.Cross;
        InvalidateNodeMove();
    }

    private bool EndEndpointMove(MouseEventArgs E)
    {
        if (_MovingRelationship is not { } Move || E.Button != MouseButtons.Left) return false;
        UpdateEndpointMove(E.Location);
        bool SourceEnd = _MovingSource;
        var Target = _EndpointTarget;
        bool Commit = _EndpointThresholdPassed && Target != null && Target != Move.Original(SourceEnd)
            && Viewport.ClientRectangle.Contains(Viewport.PointToClient(DiagramPicture.PointToScreen(E.Location)));
        CancelEndpointMove();
        if (Commit) EndpointMoveRequested?.Invoke(Move, SourceEnd, Target!);
        return true;
    }

    private bool CancelEndpointMove()
    {
        bool Active = _MovingRelationship != null;
        _MovingRelationship = null; _EndpointContext = null; _EndpointItem = null;
        _EndpointTarget = null; _EndpointThresholdPassed = false;
        if (Active)
        {
            DiagramPicture.Capture = false; DiagramPicture.Cursor = Cursors.Default; InvalidateNodeMove();
        }
        return Active;
    }

    private void PaintEndpointMove(Graphics Canvas)
    {
        var Move = _MovingRelationship ?? SelectedRelationshipMove();
        if (Move == null || !TryGetPictureTransform(out double Sx, out double Sy, out double Ox, out double Oy)) return;
        PointF Project(PointF P) => new((float)(P.X * Sx + Ox), (float)(P.Y * Sy + Oy));
        using Pen Handle = new(_SelectionColour, 2);
        foreach (bool SourceEnd in new[] { true, false })
        {
            PointF Point = Project(Move.Original(SourceEnd).Point);
            Canvas.FillEllipse(Brushes.White, Point.X - 5, Point.Y - 5, 10, 10);
            Canvas.DrawEllipse(Handle, Point.X - 5, Point.Y - 5, 10, 10);
        }
        if (_MovingRelationship == null || !_EndpointThresholdPassed) return;
        foreach (var Candidate in Move.Targets(_MovingSource))
        {
            PointF Point = Project(Candidate.Point);
            if (Math.Abs(Point.X - _EndpointPointer.X) > 100 || Math.Abs(Point.Y - _EndpointPointer.Y) > 100) continue;
            Canvas.DrawLine(Handle, Point.X - 3, Point.Y, Point.X + 3, Point.Y);
            Canvas.DrawLine(Handle, Point.X, Point.Y - 3, Point.X, Point.Y + 3);
        }
        var FixedEnd = Move.Original(!_MovingSource);
        PointF Fixed = Project(FixedEnd.Point);
        PointF Drop = _EndpointTarget is { } Target ? Project(Target.Point) : _EndpointPointer;
        using Pen Ghost = new(_EndpointTarget == null ? Color.Gray : _SelectionColour, 2) { DashStyle = DashStyle.Dash };
        PointF[] Path = [Fixed, new(Fixed.X, (Fixed.Y + Drop.Y) / 2), new(Drop.X, (Fixed.Y + Drop.Y) / 2), Drop];
        if (_EndpointTarget is { } Snapped && Snapped.NodeId == FixedEnd.NodeId
            && Index?.FindObject("node\u001f" + Snapped.NodeId) is { } Node)
        {
            var TopLeft = Project(new((float)Node.Left, (float)Node.Top));
            var BottomRight = Project(new((float)Node.Right, (float)Node.Bottom));
            Path = PreviewEndpointGhost.Loop(RectangleF.FromLTRB(TopLeft.X, TopLeft.Y, BottomRight.X, BottomRight.Y),
                Fixed, FixedEnd.Direction, Drop, Snapped.Direction);
        }
        Canvas.DrawLines(Ghost, Path);
        if (_EndpointTarget != null) Canvas.DrawEllipse(Handle, Drop.X - 7, Drop.Y - 7, 14, 14);
    }
}
