using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveEditor.Completion;

/// <summary>
/// Caret-anchored IntelliSense list for the source editor.
/// </summary>
internal sealed class CompletionPopup : IDisposable
{
    // Rich Edit EM_POSFROMCHAR (WM_USER + 38), not the plain Edit control message.
    private const int EM_POSFROMCHAR = 0x0426;

    private readonly ToolStripDropDown _DropDown;

    private readonly ListBox _List;

    private CompletionContext? _Context;

    private TextBoxBase? _Target;

    private Form? _OwnerForm;

    private bool _IsDisposed;

    private bool _OwnerEventsHooked;

    /// <summary>
    /// Creates the popup host.
    /// </summary>
    public CompletionPopup()
    {
        _List = new ListBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            IntegralHeight = false,
            ItemHeight = 22,
            Width = 440,
            Height = 180,
            DrawMode = DrawMode.Normal,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText
        };
        _List.Click += List_Click;
        _List.DoubleClick += List_DoubleClick;

        ToolStripControlHost Host = new ToolStripControlHost(_List)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = _List.Size,
            BackColor = SystemColors.Window
        };

        _DropDown = new ToolStripDropDown
        {
            AutoClose = false,
            AutoSize = false,
            DropShadowEnabled = true,
            Padding = Padding.Empty,
            BackColor = SystemColors.Window,
            Opacity = 1.0
        };
        _DropDown.Items.Add(Host);
    }

    /// <summary>Raised when the user accepts a suggestion.</summary>
    public event EventHandler<CompletionItem>? ItemAccepted;

    /// <summary>Gets whether the popup is visible.</summary>
    public bool IsVisible
    {
        get
        {
            return _DropDown.Visible;
        }
    }

    /// <summary>
    /// Shows suggestions under the caret of <paramref name="Target"/>.
    /// </summary>
    /// <param name="Target">Source editor.</param>
    /// <param name="Context">Completion context with items.</param>
    public void Show(TextBoxBase Target, CompletionContext Context)
    {
        if (Target == null || Context == null || Context.Items.Count == 0)
        {
            Hide();
            return;
        }

        _Target = Target;
        _Context = Context;
        _List.BeginUpdate();
        _List.Items.Clear();
        foreach (CompletionItem Item in Context.Items)
        {
            _List.Items.Add(Item);
        }

        _List.SelectedIndex = 0;
        if (LiveEditorFeatureGate.AB_Accessibility)
        {
            _List.AccessibleName = "Enterprise completion suggestions";
            _List.AccessibleDescription = Context.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " suggestions available";
        }

        int VisibleRows = Math.Min(8, Context.Items.Count);
        _List.Height = Math.Max(48, (VisibleRows * _List.ItemHeight) + 8);
        _List.Width = 440;
        ((ToolStripControlHost)_DropDown.Items[0]).Size = _List.Size;
        _DropDown.Size = new Size(_List.Width + 2, _List.Height + 2);
        _List.EndUpdate();

        HookOwnerEvents(Target.FindForm());
        Point ClientPoint = GetCaretClientPoint(Target);
        ClientPoint.Y += Target.Font.Height + 2;
        if (_DropDown.Visible)
        {
            Point ScreenPoint = Target.PointToScreen(ClientPoint);
            _DropDown.Left = ScreenPoint.X;
            _DropDown.Top = ScreenPoint.Y;
        }
        else
        {
            _DropDown.Show(Target, ClientPoint);
        }
    }

    /// <summary>
    /// Moves the open popup so it stays under the caret.
    /// </summary>
    public void Reposition()
    {
        if (!_DropDown.Visible || _Target == null)
        {
            return;
        }

        Point ClientPoint = GetCaretClientPoint(_Target);
        ClientPoint.Y += _Target.Font.Height + 2;
        Point ScreenPoint = _Target.PointToScreen(ClientPoint);
        _DropDown.Left = ScreenPoint.X;
        _DropDown.Top = ScreenPoint.Y;
    }

    /// <summary>
    /// Hides the popup if visible.
    /// </summary>
    public void Hide()
    {
        UnhookOwnerEvents();
        if (_DropDown.Visible)
        {
            _DropDown.Hide();
        }

        _Context = null;
        _Target = null;
    }

    /// <summary>
    /// Routes navigation keys while the popup is open.
    /// </summary>
    /// <param name="KeyData">Key data from PreviewKeyDown/KeyDown.</param>
    /// <returns>True when the key was handled.</returns>
    public bool TryHandleKey(Keys KeyData)
    {
        if (!_DropDown.Visible || _List.Items.Count == 0)
        {
            return false;
        }

        if (KeyData == Keys.Escape)
        {
            Hide();
            return true;
        }

        if (KeyData == Keys.Up)
        {
            if (_List.SelectedIndex > 0)
            {
                _List.SelectedIndex--;
            }

            return true;
        }

        if (KeyData == Keys.Down)
        {
            if (_List.SelectedIndex < _List.Items.Count - 1)
            {
                _List.SelectedIndex++;
            }

            return true;
        }

        if (KeyData == Keys.Enter || KeyData == Keys.Tab)
        {
            AcceptSelected();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Inserts the selected item into the target editor.
    /// </summary>
    public void AcceptSelected()
    {
        if (_Target == null || _Context == null || _List.SelectedItem is not CompletionItem Item)
        {
            Hide();
            return;
        }

        TextBoxBase Target = _Target;
        CompletionContext Context = _Context;
        int Start = Math.Max(0, Math.Min(Context.ReplaceStart, Target.TextLength));
        int End = Math.Max(Start, Math.Min(Context.ReplaceEnd, Target.TextLength));
        Target.Select(Start, End - Start);
        Target.SelectedText = Item.InsertText;
        Hide();
        ItemAccepted?.Invoke(this, Item);
        Target.Focus();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_IsDisposed)
        {
            return;
        }

        _IsDisposed = true;
        Hide();
        _List.Click -= List_Click;
        _List.DoubleClick -= List_DoubleClick;
        _DropDown.Dispose();
        _List.Dispose();
    }

    private void List_Click(object? Sender, EventArgs E)
    {
        AcceptSelected();
    }

    private void List_DoubleClick(object? Sender, EventArgs E)
    {
        AcceptSelected();
    }

    private void HookOwnerEvents(Form? Owner)
    {
        if (ReferenceEquals(_OwnerForm, Owner) && _OwnerEventsHooked)
        {
            return;
        }

        UnhookOwnerEvents();
        _OwnerForm = Owner;
        if (_OwnerForm == null)
        {
            return;
        }

        _OwnerForm.Move += OwnerForm_GeometryChanged;
        _OwnerForm.LocationChanged += OwnerForm_GeometryChanged;
        _OwnerForm.Resize += OwnerForm_GeometryChanged;
        _OwnerForm.ResizeEnd += OwnerForm_GeometryChanged;
        if (_Target != null)
        {
            _Target.Move += OwnerForm_GeometryChanged;
            _Target.LocationChanged += OwnerForm_GeometryChanged;
        }

        _OwnerEventsHooked = true;
    }

    private void UnhookOwnerEvents()
    {
        if (!_OwnerEventsHooked)
        {
            _OwnerForm = null;
            return;
        }

        if (_OwnerForm != null)
        {
            _OwnerForm.Move -= OwnerForm_GeometryChanged;
            _OwnerForm.LocationChanged -= OwnerForm_GeometryChanged;
            _OwnerForm.Resize -= OwnerForm_GeometryChanged;
            _OwnerForm.ResizeEnd -= OwnerForm_GeometryChanged;
        }

        if (_Target != null)
        {
            _Target.Move -= OwnerForm_GeometryChanged;
            _Target.LocationChanged -= OwnerForm_GeometryChanged;
        }

        _OwnerEventsHooked = false;
        _OwnerForm = null;
    }

    private void OwnerForm_GeometryChanged(object? Sender, EventArgs E)
    {
        Reposition();
    }

    private static Point GetCaretClientPoint(TextBoxBase Target)
    {
        int Index = Math.Max(0, Math.Min(Target.SelectionStart, Target.TextLength));
        if (Target is RichTextBox Rich && Rich.IsHandleCreated)
        {
            Point FromMessage = new Point();
            IntPtr Result = SendMessage(Rich.Handle, EM_POSFROMCHAR, (IntPtr)Index, IntPtr.Zero);
            if (Result != IntPtr.Zero || Index == 0)
            {
                int Packed = Result.ToInt32();
                FromMessage.X = (short)(Packed & 0xFFFF);
                FromMessage.Y = (short)((Packed >> 16) & 0xFFFF);
                if (FromMessage.X != 0 || FromMessage.Y != 0 || Index == 0)
                {
                    return FromMessage;
                }
            }
        }

        if (Index <= 0)
        {
            return new Point(2, 2);
        }

        Point Point = Target.GetPositionFromCharIndex(Index);
        if (Point.X == 0 && Point.Y == 0)
        {
            Point = Target.GetPositionFromCharIndex(Index - 1);
            using Graphics Graphics = Target.CreateGraphics();
            string Ch = Target.Text.Substring(Index - 1, 1);
            SizeF Size = Graphics.MeasureString(Ch, Target.Font);
            Point.X += (int)Math.Ceiling(Size.Width);
        }

        return Point;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr HWnd, int Msg, IntPtr WParam, IntPtr LParam);
}
