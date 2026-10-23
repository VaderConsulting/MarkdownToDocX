using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor.Completion;

/// <summary>
/// Caret-anchored IntelliSense list for the source editor.
/// </summary>
internal sealed class CompletionPopup : IDisposable
{
    private readonly ToolStripDropDown _DropDown;

    private readonly ListBox _List;

    private CompletionContext? _Context;

    private TextBox? _Target;

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
            Width = 420,
            Height = 180,
            DrawMode = DrawMode.OwnerDrawFixed
        };
        _List.DrawItem += List_DrawItem;
        _List.Click += List_Click;
        _List.DoubleClick += List_DoubleClick;

        ToolStripControlHost Host = new ToolStripControlHost(_List)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = _List.Size
        };

        _DropDown = new ToolStripDropDown
        {
            AutoClose = false,
            AutoSize = false,
            DropShadowEnabled = true,
            Padding = Padding.Empty
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
    public void Show(TextBox Target, CompletionContext Context)
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
        int VisibleRows = Math.Min(8, Context.Items.Count);
        _List.Height = Math.Max(44, VisibleRows * _List.ItemHeight + 4);
        ((ToolStripControlHost)_DropDown.Items[0]).Size = _List.Size;
        _List.EndUpdate();

        HookOwnerEvents(Target.FindForm());
        Point ScreenPoint = GetCaretScreenPoint(Target);
        if (_DropDown.Visible)
        {
            _DropDown.Left = ScreenPoint.X;
            _DropDown.Top = ScreenPoint.Y;
        }
        else
        {
            _DropDown.Show(ScreenPoint);
        }
    }

    /// <summary>
    /// Moves the open popup so it stays under the caret (for example after the host window moves).
    /// </summary>
    public void Reposition()
    {
        if (!_DropDown.Visible || _Target == null)
        {
            return;
        }

        MoveToCaret();
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

        TextBox Target = _Target;
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
        _List.DrawItem -= List_DrawItem;
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

    private void List_DrawItem(object? Sender, DrawItemEventArgs E)
    {
        E.DrawBackground();
        if (E.Index < 0 || E.Index >= _List.Items.Count)
        {
            return;
        }

        CompletionItem Item = (CompletionItem)_List.Items[E.Index];
        bool Selected = (E.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color Primary = Selected ? SystemColors.HighlightText : SystemColors.WindowText;
        Color Secondary = Selected ? Color.FromArgb(220, 220, 220) : SystemColors.GrayText;
        using SolidBrush PrimaryBrush = new SolidBrush(Primary);
        using SolidBrush SecondaryBrush = new SolidBrush(Secondary);
        Rectangle Bounds = E.Bounds;
        E.Graphics.DrawString(Item.DisplayText, _List.Font, PrimaryBrush, Bounds.Left + 6, Bounds.Top + 2);
        if (!string.IsNullOrWhiteSpace(Item.Detail))
        {
            SizeF PrimarySize = E.Graphics.MeasureString(Item.DisplayText, _List.Font);
            float DetailX = Bounds.Left + 12 + PrimarySize.Width;
            E.Graphics.DrawString(Item.Detail, _List.Font, SecondaryBrush, DetailX, Bounds.Top + 2);
        }

        E.DrawFocusRectangle();
    }

    private void MoveToCaret()
    {
        if (_Target == null)
        {
            return;
        }

        Point ScreenPoint = GetCaretScreenPoint(_Target);
        _DropDown.Left = ScreenPoint.X;
        _DropDown.Top = ScreenPoint.Y;
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

    private static Point GetCaretScreenPoint(TextBox Target)
    {
        Point CaretClient = GetCaretClientPoint(Target);
        return Target.PointToScreen(new Point(CaretClient.X, CaretClient.Y + Target.Font.Height + 2));
    }

    private static Point GetCaretClientPoint(TextBox Target)
    {
        int Index = Target.SelectionStart;
        if (Index <= 0)
        {
            return new Point(2, 2);
        }

        Point Point = Target.GetPositionFromCharIndex(Index);
        if (Point.X == 0 && Point.Y == 0 && Index > 0)
        {
            Point = Target.GetPositionFromCharIndex(Index - 1);
            using Graphics Graphics = Target.CreateGraphics();
            string Ch = Target.Text.Substring(Index - 1, 1);
            SizeF Size = Graphics.MeasureString(Ch, Target.Font);
            Point.X += (int)Math.Ceiling(Size.Width);
        }

        return Point;
    }
}
