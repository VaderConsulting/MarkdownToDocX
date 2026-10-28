using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveEditor;

/// <summary>Retries splitter painting when .NET 10 layout meets an unavailable display surface.</summary>
internal sealed class ResilientSplitContainer : SplitContainer
{
    protected override void OnLayout(LayoutEventArgs E)
    {
        try { base.OnLayout(E); }
        catch (ExternalException Error) when (IsSplitterPaintFailure(Error))
        {
            // RepaintSplitterRect uses an immediate GDI+ surface, which can fail
            // during display-session transitions. Its geometry is already set;
            // retry normal painting without entering WinForms' modal error UI.
            Invalidate(SplitterRectangle);
        }
    }

    private static bool IsSplitterPaintFailure(ExternalException Error) => Error.ErrorCode == unchecked((int)0x80004005)
        && new StackTrace(Error, false).GetFrames().Any(Frame => Frame.GetMethod() is { } Method
            && Method.DeclaringType == typeof(SplitContainer) && Method.Name == "RepaintSplitterRect");
}
