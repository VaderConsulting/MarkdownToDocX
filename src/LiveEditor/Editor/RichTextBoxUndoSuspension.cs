using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveEditor.Editor;

/// <summary>
/// Stops colouring from being recorded as undo steps.
/// </summary>
internal sealed partial class RichTextBoxUndoSuspension : IDisposable
{
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr Handle, int Message, IntPtr WParam, ref IntPtr LParam);

    private const int WM_USER = 0x0400;

    private const int EM_GETOLEINTERFACE = WM_USER + 60;

    private const int TOM_SUSPEND = -9999995;

    private const int TOM_RESUME = -9999994;

    private readonly object? _Document;

    private RichTextBoxUndoSuspension(object? Document)
    {
        _Document = Document;
        InvokeUndo(TOM_SUSPEND);
    }

    /// <summary>Suspends undo recording until this value is disposed.</summary>
    public static RichTextBoxUndoSuspension Suspend(RichTextBox Editor)
    {
        return new RichTextBoxUndoSuspension(TryGetDocument(Editor));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        InvokeUndo(TOM_RESUME);
    }

    private void InvokeUndo(int Command)
    {
        if (_Document == null)
        {
            return;
        }

        try
        {
            _Document.GetType().InvokeMember(
                "Undo",
                BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public,
                null,
                _Document,
                [Command]);
        }
        catch (TargetInvocationException)
        {
        }
        catch (COMException)
        {
        }
        catch (MissingMethodException)
        {
        }
    }

    private static object? TryGetDocument(RichTextBox Editor)
    {
        if (Editor == null || Editor.IsDisposed || !Editor.IsHandleCreated)
        {
            return null;
        }

        IntPtr OlePointer = IntPtr.Zero;
        try
        {
            SendMessage(Editor.Handle, EM_GETOLEINTERFACE, IntPtr.Zero, ref OlePointer);
            if (OlePointer == IntPtr.Zero)
            {
                return null;
            }

            Guid DocumentId = new Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D");
            int Query = Marshal.QueryInterface(OlePointer, in DocumentId, out IntPtr DocumentPointer);
            if (Query != 0 || DocumentPointer == IntPtr.Zero)
            {
                return null;
            }

            object Document = Marshal.GetUniqueObjectForIUnknown(DocumentPointer);
            Marshal.Release(DocumentPointer);
            return Document;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (OlePointer != IntPtr.Zero)
            {
                Marshal.Release(OlePointer);
            }
        }
    }

}
