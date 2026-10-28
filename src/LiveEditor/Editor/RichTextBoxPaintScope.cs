using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveEditor.Editor;

/// <summary>
/// Freezes RichTextBox painting and preserves scroll position while colouring text.
/// </summary>
internal sealed class RichTextBoxPaintScope : IDisposable
{
    private const int WM_SETREDRAW = 0x000B;

    private const int EM_GETSCROLLPOS = 0x04DD;

    private const int EM_SETSCROLLPOS = 0x04DE;

    private readonly RichTextBox _Editor;

    private readonly int _SelectionStart;

    private readonly int _SelectionLength;

    private readonly Point _ScrollPos;

    private bool _Disposed;

    public RichTextBoxPaintScope(RichTextBox Editor)
    {
        _Editor = Editor ?? throw new ArgumentNullException(nameof(Editor));
        _SelectionStart = _Editor.SelectionStart;
        _SelectionLength = _Editor.SelectionLength;
        _ScrollPos = GetScrollPos(_Editor);
        SendMessage(_Editor.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        int Start = Math.Max(0, Math.Min(_SelectionStart, _Editor.TextLength));
        int Length = Math.Max(0, Math.Min(_SelectionLength, _Editor.TextLength - Start));
        _Editor.Select(Start, Length);
        SetScrollPos(_Editor, _ScrollPos);
        SendMessage(_Editor.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
        _Editor.Invalidate();
    }

    private static Point GetScrollPos(RichTextBox Editor)
    {
        Point Point = new Point();
        SendMessage(Editor.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref Point);
        return Point;
    }

    private static void SetScrollPos(RichTextBox Editor, Point Point)
    {
        Point Copy = Point;
        SendMessage(Editor.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref Copy);
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern IntPtr SendMessage(IntPtr HWnd, int Msg, IntPtr WParam, IntPtr LParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern IntPtr SendMessage(IntPtr HWnd, int Msg, IntPtr WParam, ref Point LParam);
}
