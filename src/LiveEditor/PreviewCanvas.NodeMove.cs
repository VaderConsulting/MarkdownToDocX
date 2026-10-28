using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveEditor;

internal sealed partial class PreviewCanvas
{
    // The host checks source/session freshness both at press and release.
    internal Func<PreviewObject, bool>? CanMoveNode;
    internal Func<PreviewObject, Func<PointF, PointF?>?>? CreateNodeMoveSnap;
    internal event Action<string, PointF>? NodeMoveRequested;
    private PreviewObject? _MovingNode;
    private Point _MoveStart;
    private bool _MoveThresholdPassed;
    private Func<PointF, PointF?>? _SnapMove;
    private PointF? _MoveDrop;
    internal bool IsMovingNode => _MovingNode != null;

    private void BeginNodeMove(PreviewObject? Hit, Point Pointer, bool Modified)
    {
        CancelNodeMove();
        if (Modified || Hit?.Kind != PreviewObjectKind.Node || CanMoveNode?.Invoke(Hit) != true
            || !TryGetPictureTransform(out _, out _, out _, out _)) return;
        _SnapMove = CreateNodeMoveSnap?.Invoke(Hit);
        if (_SnapMove == null) return;
        _MovingNode = Hit;
        _MoveStart = Pointer;
        DiagramPicture.Capture = true;
    }

    private void UpdateNodeMove(Point Pointer)
    {
        if (_MovingNode == null) return;
        Size Threshold = SystemInformation.DragSize;
        _MoveThresholdPassed |= Math.Abs(Pointer.X - _MoveStart.X) > Threshold.Width / 2
            || Math.Abs(Pointer.Y - _MoveStart.Y) > Threshold.Height / 2;
        if (TryGetPictureTransform(out double Sx, out double Sy, out _, out _))
            _MoveDrop = _SnapMove?.Invoke(new(
                (float)((_MovingNode.Left + _MovingNode.Right) / 2 + (Pointer.X - _MoveStart.X) / Sx),
                (float)((_MovingNode.Top + _MovingNode.Bottom) / 2 + (Pointer.Y - _MoveStart.Y) / Sy)));
        if (_MoveThresholdPassed) DiagramPicture.Cursor = Cursors.SizeAll;
        InvalidateNodeMove();
    }

    private void Picture_MouseUp(object? Sender, MouseEventArgs E)
    {
        if (EndEndpointMove(E)) return;
        if (E.Button != MouseButtons.Left || _MovingNode == null) return;
        UpdateNodeMove(E.Location);
        PreviewObject Moving = _MovingNode;
        bool Commit = _MoveThresholdPassed && _MoveDrop.HasValue && CanMoveNode?.Invoke(Moving) == true
            && Viewport.ClientRectangle.Contains(Viewport.PointToClient(DiagramPicture.PointToScreen(E.Location)));
        PointF Drop = _MoveDrop.GetValueOrDefault();
        CancelNodeMove();
        if (Commit) NodeMoveRequested?.Invoke(Moving.Id, Drop);
    }

    private void Picture_MouseCaptureChanged(object? Sender, EventArgs E)
    {
        if (!DiagramPicture.Capture) CancelNodeMove();
    }

    internal bool CancelNodeMove()
    {
        bool EndpointCancelled = CancelEndpointMove();
        if (_MovingNode == null) return EndpointCancelled;
        _MovingNode = null;
        _SnapMove = null;
        _MoveDrop = null;
        _MoveThresholdPassed = false;
        DiagramPicture.Capture = false;
        DiagramPicture.Cursor = Cursors.Default;
        InvalidateNodeMove();
        return true;
    }

    private void InvalidateNodeMove()
    {
        // The picture owns capture, but the visible drag surface is the entire
        // viewport. Repaint both so an outline crossing the image edge stays
        // continuous, and previous feedback is erased on move/cancellation.
        DiagramPicture.Invalidate();
        Viewport.Invalidate();
    }

    private void Viewport_Paint(object? Sender, PaintEventArgs E)
    {
        if (!IsMovingNode && !IsMovingEndpoint) return;
        Point Origin = Viewport.PointToClient(DiagramPicture.PointToScreen(Point.Empty));
        GraphicsState Saved = E.Graphics.Save();
        try
        {
            E.Graphics.TranslateTransform(Origin.X, Origin.Y);
            PaintNodeMove(E.Graphics);
            PaintEndpointMove(E.Graphics);
        }
        finally { E.Graphics.Restore(Saved); }
    }

    private void PaintNodeMove(Graphics Canvas)
    {
        if (_MovingNode == null || !_MoveThresholdPassed || _MoveDrop is not { } Drop
            || !TryGetPictureTransform(out double Sx, out double Sy, out double Ox, out double Oy)) return;
        using Pen Outline = new(_SelectionColour, 2) { DashStyle = DashStyle.Dash };
        // Separate feedback from the solid selection border even when the
        // pointer has moved but snapping still selects the original cell.
        const float Margin = 4;
        Canvas.DrawRectangle(Outline,
            (float)((Drop.X - (_MovingNode.Right - _MovingNode.Left) / 2) * Sx + Ox) - Margin,
            (float)((Drop.Y - (_MovingNode.Bottom - _MovingNode.Top) / 2) * Sy + Oy) - Margin,
            (float)((_MovingNode.Right - _MovingNode.Left) * Sx) + 2 * Margin,
            (float)((_MovingNode.Bottom - _MovingNode.Top) * Sy) + 2 * Margin);
    }
}
