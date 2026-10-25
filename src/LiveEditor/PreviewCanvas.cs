using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MDConverter;

namespace LiveEditor;

/// <summary>
/// Scrollable diagram picture with zoom controls, fit, and Visio-style wheel navigation.
/// </summary>
internal sealed partial class PreviewCanvas : UserControl
{
    private const double MINIMUM_ZOOM = 0.10;

    private const double MAXIMUM_ZOOM = 8.00;

    private const double ZOOM_STEP = 1.20;

    private const int WM_MOUSEWHEEL = 0x020A;

    private double _Zoom = 1.0;

    private bool _FitToWindow = true;

    private Rectangle _FitObstacle;

    private bool _NavigationEnabled = true;

    private bool _PositioningZoomButtons;

    private PreviewHoverIndex? _Index;

    private readonly List<PreviewObject> _SelectedItems = new List<PreviewObject>();

    private static readonly Color DEFAULT_SELECTION_COLOUR = Color.FromArgb(0, 122, 204);

    private Color _SelectionColour;

    private PreviewStyle _SelectionStyle = null!;

    private readonly List<PreviewObject> _NoItems = new List<PreviewObject>();

    private readonly List<PreviewError> _Errors = new List<PreviewError>();

    private bool _UseLegacyRenderer;

    private bool _Applying;

    private int _LoadSequence;

    private string? _RenderedSvg;

    private string? _RenderFailure;

    /// <summary>
    /// Creates the preview and wires its picture, wheel, and zoom buttons.
    /// </summary>
    public PreviewCanvas()
    {
        _SelectionColour = DEFAULT_SELECTION_COLOUR;
        _SelectionStyle = PreviewStyle.ForCurrentDiagram(DEFAULT_SELECTION_COLOUR);
        InitializeComponent();
    }

    /// <summary>Raised when the pointer moves over the picture.</summary>
    public event MouseEventHandler? PictureMouseMove;

    /// <summary>Raised when the pointer leaves the picture or the viewport.</summary>
    public event EventHandler? PictureMouseLeave;

    /// <summary>Raised when a mouse button goes down on the picture.</summary>
    public event MouseEventHandler? PictureMouseDown;

    /// <summary>Raised when the picture paints, after the image itself.</summary>
    public event PaintEventHandler? PicturePaint;

    /// <summary>Raised when Esc is pressed while the preview has focus.</summary>
    public event EventHandler? EscapePressed;

    /// <summary>Raised when Up or Down is pressed while the preview has focus.</summary>
    public event EventHandler<KeyEventArgs>? LineStepRequested;

    /// <summary>Raised after the zoom factor or fit mode changes.</summary>
    public event EventHandler? ZoomChanged;

    /// <summary>Raised when Refresh is chosen from the diagram menu. The host redraws the picture from the script.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Raised when <see cref="SelectedItems"/> changes.</summary>
    public event EventHandler? SelectedItemsChanged;

    /// <summary>Gets the rendered diagram, or null when the script or the items have not produced a picture.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get
        {
            return DiagramPicture.Image;
        }
    }

    /// <summary>Gets the rendered diagram, or null when the script or the items have not produced a picture.</summary>
    public Image? Picture
    {
        get
        {
            return Image;
        }
    }

    /// <summary>Gets whether a diagram image is assigned.</summary>
    public bool HasPicture
    {
        get
        {
            return Image != null;
        }
    }

    /// <summary>Gets whether the picture box has been disposed.</summary>
    public bool IsPictureDisposed
    {
        get
        {
            return DiagramPicture.IsDisposed;
        }
    }

    /// <summary>Gets the selectable objects in the current diagram. Empty until a script is loaded or <see cref="AddItem()"/> is called.</summary>
    public IReadOnlyList<PreviewObject> Items
    {
        get
        {
            if (_Index == null)
            {
                return _NoItems;
            }

            return _Index.Objects;
        }
    }

    /// <summary>
    /// Gets the errors for the current diagram.
    /// Each error carries its description and the nodes and relationships it involves.
    /// A script load replaces the list.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<PreviewError> Errors
    {
        get
        {
            return _Errors;
        }
    }

    /// <summary>Gets the hit index for the loaded picture, or null when nothing is loaded.</summary>
    internal PreviewHoverIndex? Index
    {
        get
        {
            return _Index;
        }
    }

    /// <summary>
    /// Gets or sets the selected items, in the order they were selected.
    /// Null when nothing is selected. Assigning null clears the selection. Ctrl-click toggles one item and leaves the others selected.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<PreviewObject>? SelectedItems
    {
        get
        {
            if (_SelectedItems.Count == 0)
            {
                return null;
            }

            return _SelectedItems;
        }

        set
        {
            AssignSelectedItems(value);
        }
    }

    /// <summary>Gets or sets the colour used to draw the selection on the canvas.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectionColour
    {
        get
        {
            return _SelectionColour;
        }

        set
        {
            _SelectionColour = value;
            DiagramPicture.Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the style whose values describe a selected item.
    /// It starts with the same line colour, bold stroke and width as the diagram selection, and then stands on its own.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PreviewStyle SelectionStyle
    {
        get
        {
            return _SelectionStyle;
        }

        set
        {
            PreviewStyle Next = value ?? PreviewStyle.ForCurrentDiagram(DEFAULT_SELECTION_COLOUR);
            if (ReferenceEquals(_SelectionStyle, Next))
            {
                return;
            }

            _SelectionStyle = Next;
        }
    }

    /// <summary>Gets or sets the stored zoom level. 1 is 100 percent. Fit-to-window does not change this.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Zoom
    {
        get
        {
            return _Zoom;
        }

        set
        {
            _Zoom = ClampZoom(value);
        }
    }

    /// <summary>Gets or sets whether the picture is scaled to the viewport.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FitToWindow
    {
        get
        {
            return _FitToWindow;
        }

        set
        {
            _FitToWindow = value;
        }
    }

    /// <summary>Gets or sets whether zoom buttons and the wheel may change the view.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool NavigationEnabled
    {
        get
        {
            return _NavigationEnabled;
        }

        set
        {
            _NavigationEnabled = value;
            ZoomInButton.Enabled = value;
            ZoomOutButton.Enabled = value;
            FitButton.Enabled = value;
        }
    }

    /// <summary>
    /// Gets or sets whether the next script render uses the legacy Naiad picture.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool UseLegacyRenderer
    {
        get
        {
            return _UseLegacyRenderer;
        }

        set
        {
            _UseLegacyRenderer = value;
        }
    }

    /// <summary>Gets the SVG the current image was drawn from, or null.</summary>
    internal string? RenderedSvg
    {
        get
        {
            return _RenderedSvg;
        }
    }

    /// <summary>Gets why the last render produced no image, or null when the picture is current.</summary>
    internal string? RenderFailure
    {
        get
        {
            return _RenderFailure;
        }
    }

    /// <summary>
    /// Builds the items and errors from a diagram script and renders the image.
    /// </summary>
    /// <param name="Script">Diagram script.</param>
    public new void Load(string? Script)
    {
        int Sequence = Interlocked.Increment(ref _LoadSequence);
        string Source = Script ?? string.Empty;
        Prepare(Source, out string? Svg, out byte[]? Png, out string? Failure);
        Action Apply = () =>
        {
            if (Sequence != Volatile.Read(ref _LoadSequence))
            {
                return;
            }

            ApplyPrepared(Source, Svg, Png, Failure);
        };
        if (IsHandleCreated && InvokeRequired)
        {
            Invoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    /// <summary>
    /// Adds a blank item. Set its properties to describe the node or relationship.
    /// </summary>
    /// <returns>The new item.</returns>
    public PreviewObject AddItem()
    {
        PreviewObject Item = new PreviewObject();
        AddItem(Item);
        return Item;
    }

    /// <summary>
    /// Adds an item that has already been created.
    /// </summary>
    /// <param name="Item">Item to add.</param>
    public void AddItem(PreviewObject Item)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        EnsureIndex();
        _Index!.AddObject(Item);
        WatchItem(Item);
        RenderPictureFromItems();
    }

    /// <summary>
    /// Removes an item, clears it if it was selected, and drops it from every error.
    /// </summary>
    /// <param name="Item">Item to remove.</param>
    /// <returns>False when the item was not in the diagram.</returns>
    public bool RemoveItem(PreviewObject Item)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (_Index == null || !_Index.RemoveObject(Item))
        {
            return false;
        }

        foreach (PreviewError Error in _Errors)
        {
            Error.Nodes.Remove(Item);
            Error.Relationships.Remove(Item);
        }

        UnwatchItem(Item);
        bool WasSelected = _SelectedItems.Remove(Item);
        Item.Selected = false;
        if (WasSelected)
        {
            DiagramPicture.Invalidate();
            SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        RenderPictureFromItems();
        return true;
    }

    /// <summary>
    /// Clears the picture, the items, the errors, and the selection.
    /// </summary>
    public void Clear()
    {
        Interlocked.Increment(ref _LoadSequence);
        DetachItems();
        ClearBitmap();
        _RenderedSvg = null;
        _RenderFailure = null;
        _Index = null;
        _Errors.Clear();
        AssignSelectedItems(null);
        ApplyView();
    }

    private void Prepare(string Source, out string? Svg, out byte[]? Png, out string? Failure)
    {
        Svg = null;
        Png = null;
        Failure = null;
        if (string.IsNullOrWhiteSpace(Source))
        {
            return;
        }

        try
        {
            bool Rendered;
            if (_UseLegacyRenderer)
            {
                Svg = MermaidDiagramExport.RenderSvg(Source);
                Rendered = !string.IsNullOrWhiteSpace(Svg);
            }
            else
            {
                Rendered = MermaidDiagramExport.TryRenderPreviewSvg(Source, out Svg);
            }

            if (!Rendered || string.IsNullOrWhiteSpace(Svg))
            {
                Svg = null;
                Failure = "Incomplete diagram";
                return;
            }

            Png = MermaidDiagramExport.TryRasterizeSvg(Svg);
            if (Png == null || Png.Length == 0)
            {
                Svg = null;
                Png = null;
                Failure = "Incomplete diagram";
            }
        }
        catch (Exception Ex)
        {
            Svg = null;
            Png = null;
            Failure = string.IsNullOrWhiteSpace(Ex.Message) ? "Incomplete diagram" : Ex.Message;
        }
    }

    private void ApplyPrepared(string Source, string? Svg, byte[]? Png, string? Failure)
    {
        _Applying = true;
        try
        {
            List<string> SelectedKeys = new List<string>();
            foreach (PreviewObject Selected in _SelectedItems)
            {
                SelectedKeys.Add(Selected.Key);
            }

            DetachItems();
            if (Png != null && Png.Length > 0 && !string.IsNullOrWhiteSpace(Svg))
            {
                ShowPng(Png);
                _RenderedSvg = Svg;
                _RenderFailure = null;
                _Index = PreviewHoverIndex.Build(Svg, Source);
            }
            else
            {
                ClearBitmap();
                _RenderedSvg = null;
                _RenderFailure = Failure;
                _Index = PreviewHoverIndex.FromScript(Source);
            }

            ReplaceErrors(PreviewError.FromScript(_Index.Objects, Source));
            AttachItems();
            List<PreviewObject> Restored = new List<PreviewObject>();
            foreach (string Key in SelectedKeys)
            {
                PreviewObject? Found = _Index.FindObject(Key);
                if (Found != null && !ContainsReference(Restored, Found))
                {
                    Restored.Add(Found);
                }
            }

            AssignSelectedItems(Restored);
            ApplyView();
        }
        finally
        {
            _Applying = false;
        }
    }

    private void RenderPictureFromItems()
    {
        if (_Applying || _Index == null)
        {
            return;
        }

        int Sequence = Interlocked.Increment(ref _LoadSequence);
        string Source = ComposeScript(_Index.Objects);
        try
        {
            Prepare(Source, out string? Svg, out byte[]? Png, out string? Failure);
            Action Apply = () =>
            {
                if (Sequence != Volatile.Read(ref _LoadSequence) || _Applying)
                {
                    return;
                }

                if (Png != null && Png.Length > 0 && !string.IsNullOrWhiteSpace(Svg))
                {
                    ShowPng(Png);
                    _RenderedSvg = Svg;
                    _RenderFailure = null;
                }
                else
                {
                    ClearBitmap();
                    _RenderedSvg = null;
                    _RenderFailure = Failure;
                }

                ApplyView();
            };
            if (IsHandleCreated && InvokeRequired)
            {
                Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }
        catch (Exception Ex)
        {
            ClearBitmap();
            _RenderedSvg = null;
            _RenderFailure = string.IsNullOrWhiteSpace(Ex.Message) ? "Incomplete diagram" : Ex.Message;
        }
    }

    private static string ComposeScript(IReadOnlyList<PreviewObject> Items)
    {
        StringBuilder Text = new StringBuilder();
        Text.Append("enterprise\n");
        foreach (PreviewObject Item in Items)
        {
            if (Item.Kind == PreviewObjectKind.Relationship)
            {
                if (Item.SourceId.Length == 0 || Item.TargetId.Length == 0 || Item.RelationshipKind.Length == 0)
                {
                    continue;
                }

                Text.Append(Item.SourceId);
                if (string.Equals(Item.RelationshipKind, "connection", StringComparison.Ordinal))
                {
                    Text.Append(" --- ");
                }
                else
                {
                    Text.Append(" -");
                    Text.Append(Item.RelationshipKind);
                    Text.Append("-> ");
                }

                Text.Append(Item.TargetId);
                Text.Append('\n');
            }
            else if (Item.Id.Length > 0)
            {
                string TypeName = Item.Kind == PreviewObjectKind.Container ? "group" : "application-component";
                Text.Append(TypeName);
                Text.Append(' ');
                Text.Append(Item.Id);
                Text.Append(" \"");
                Text.Append(Item.Id.Replace("\"", "'", StringComparison.Ordinal));
                Text.Append("\"\n");
            }
        }

        return Text.ToString();
    }

    private void OnItemChanged()
    {
        RenderPictureFromItems();
    }

    private void DetachItems()
    {
        if (_Index == null)
        {
            return;
        }

        foreach (PreviewObject Item in _Index.Objects)
        {
            UnwatchItem(Item);
            Item.Selected = false;
        }
    }

    private void AttachItems()
    {
        if (_Index == null)
        {
            return;
        }

        foreach (PreviewObject Item in _Index.Objects)
        {
            WatchItem(Item);
        }
    }

    private void WatchItem(PreviewObject Item)
    {
        Item.Changed = OnItemChanged;
        Item.StyleChanged = () => OnItemStyleChanged(Item);
        Item.AppearanceChanged = () => DiagramPicture.Invalidate();
    }

    private void UnwatchItem(PreviewObject Item)
    {
        Item.Changed = null;
        Item.StyleChanged = null;
        Item.AppearanceChanged = null;
    }

    private void OnItemStyleChanged(PreviewObject Item)
    {
        if (Item.Selected)
        {
            return;
        }

        DiagramPicture.Invalidate();
    }

    private void ShowPng(byte[] Png)
    {
        using MemoryStream Stream = new MemoryStream(Png);
        using Image Loaded = Image.FromStream(Stream);
        Bitmap Copy = new Bitmap(Loaded);
        Image? Previous = DiagramPicture.Image;
        DiagramPicture.Image = Copy;
        if (Previous != null && !ReferenceEquals(Previous, Copy))
        {
            Previous.Dispose();
        }
    }

    private void ClearBitmap()
    {
        Image? Previous = DiagramPicture.Image;
        DiagramPicture.Image = null;
        if (Previous != null)
        {
            Previous.Dispose();
        }
    }

    private void EnsureIndex()
    {
        if (_Index == null)
        {
            _Index = PreviewHoverIndex.CreateEmpty();
        }
    }

    private void ReplaceErrors(List<PreviewError> Errors)
    {
        _Errors.Clear();
        _Errors.AddRange(Errors);
    }

    /// <summary>
    /// Lays the picture out again and paints the selection.
    /// </summary>
    public new void Refresh()
    {
        ApplyView();
        Invalidate(true);
        Update();
    }

    /// <summary>
    /// Returns the item under a point in the picture.
    /// </summary>
    /// <param name="PictureClient">Point in picture-box client pixels.</param>
    /// <returns>The item, or null for empty space.</returns>
    public PreviewObject? HitItem(Point PictureClient)
    {
        if (!TryGetPictureTransform(out double ScaleX, out double ScaleY, out double OffsetX, out double OffsetY))
        {
            return null;
        }

        double SvgX = (PictureClient.X - OffsetX) / ScaleX;
        double SvgY = (PictureClient.Y - OffsetY) / ScaleY;
        return _Index!.HitObject(SvgX, SvgY, PreviewSelection.CLICK_TOLERANCE / Math.Min(ScaleX, ScaleY));
    }

    /// <summary>
    /// Scrolls the viewport so the item is centred. When <paramref name="Centre"/> is false, an item already
    /// inside the viewport is left where it is.
    /// </summary>
    /// <param name="Item">Item to bring into view.</param>
    /// <param name="Centre">True to centre the item even when it is already visible.</param>
    internal void ScrollItemIntoView(PreviewObject Item, bool Centre)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (!TryGetItemViewportBounds(Item, out Rectangle Bounds))
        {
            return;
        }

        if (!Centre && Viewport.ClientRectangle.Contains(Bounds))
        {
            return;
        }

        int ScrollX = -Viewport.AutoScrollPosition.X;
        int ScrollY = -Viewport.AutoScrollPosition.Y;
        int ItemCentreX = Bounds.X + (Bounds.Width / 2);
        int ItemCentreY = Bounds.Y + (Bounds.Height / 2);
        int NextX = ScrollX + ItemCentreX - (Viewport.ClientSize.Width / 2);
        int NextY = ScrollY + ItemCentreY - (Viewport.ClientSize.Height / 2);
        Viewport.AutoScrollPosition = new Point(Math.Max(0, NextX), Math.Max(0, NextY));
    }

    /// <summary>
    /// Reports whether a picture-box point lies on the rendered image rather than the margin around it.
    /// </summary>
    /// <param name="PictureClient">Point in picture-box client pixels.</param>
    /// <returns>False when the point is outside the drawn diagram.</returns>
    internal bool IsOnDiagram(Point PictureClient)
    {
        Image? Shown = DiagramPicture.Image;
        if (Shown == null || Shown.Width <= 0 || Shown.Height <= 0)
        {
            return false;
        }

        int ControlWidth = DiagramPicture.ClientSize.Width;
        int ControlHeight = DiagramPicture.ClientSize.Height;
        if (ControlWidth <= 0 || ControlHeight <= 0)
        {
            return false;
        }

        double Scale = Math.Min(ControlWidth / (double)Shown.Width, ControlHeight / (double)Shown.Height);
        if (Scale <= 0)
        {
            return false;
        }

        double DrawnWidth = Shown.Width * Scale;
        double DrawnHeight = Shown.Height * Scale;
        double Left = (ControlWidth - DrawnWidth) / 2.0;
        double Top = (ControlHeight - DrawnHeight) / 2.0;
        return PictureClient.X >= Left
            && PictureClient.Y >= Top
            && PictureClient.X < Left + DrawnWidth
            && PictureClient.Y < Top + DrawnHeight;
    }

    /// <summary>
    /// Returns the hover text under a point in the picture.
    /// </summary>
    /// <param name="PictureClient">Point in picture-box client pixels.</param>
    /// <returns>The text, or null when the point hits nothing.</returns>
    public string? HitText(Point PictureClient)
    {
        if (!TryGetPictureTransform(out double ScaleX, out double ScaleY, out double OffsetX, out double OffsetY))
        {
            return null;
        }

        double SvgX = (PictureClient.X - OffsetX) / ScaleX;
        double SvgY = (PictureClient.Y - OffsetY) / ScaleY;
        return _Index!.Hit(SvgX, SvgY);
    }

    /// <summary>
    /// Finds an item by the key from an earlier render.
    /// </summary>
    /// <param name="Key">Item key.</param>
    /// <returns>The item, or null.</returns>
    public PreviewObject? FindItem(string? Key)
    {
        if (_Index == null)
        {
            return null;
        }

        return _Index.FindObject(Key);
    }

    /// <summary>
    /// Finds the item declared on a source line.
    /// </summary>
    /// <param name="LineNumber">1-based editor line.</param>
    /// <returns>The item, or null.</returns>
    public PreviewObject? ItemAtLine(int LineNumber)
    {
        if (_Index == null)
        {
            return null;
        }

        return _Index.ObjectAtLine(LineNumber);
    }

    /// <summary>
    /// Shows a message across the top of the preview.
    /// </summary>
    /// <param name="Text">Message text.</param>
    /// <param name="Height">Banner height in pixels.</param>
    public void ShowBanner(string Text, int Height)
    {
        Banner.Text = Text;
        Banner.Height = Height;
        Banner.Visible = true;
        PerformLayout();
    }

    /// <summary>Hides the message banner.</summary>
    public void HideBanner()
    {
        Banner.Visible = false;
        Banner.Height = 0;
        Banner.Text = string.Empty;
        PerformLayout();
    }

    /// <summary>
    /// Applies diagram and banner colours.
    /// </summary>
    /// <param name="DarkTheme">Whether the dark palette is in use.</param>
    /// <param name="DiagramBackground">Background behind the diagram.</param>
    /// <param name="Chrome">Background of the control around the diagram.</param>
    public void ApplyChrome(bool DarkTheme, Color DiagramBackground, Color Chrome)
    {
        Viewport.BackColor = DiagramBackground;
        DiagramPicture.BackColor = DiagramBackground;
        BackColor = Chrome;
        Banner.BackColor = DarkTheme ? Color.FromArgb(100, 70, 20) : Color.LemonChiffon;
        Banner.ForeColor = DarkTheme ? Color.White : Color.Black;
    }

    /// <summary>
    /// Sets a bottom-left region (preview client coordinates) kept clear when fitting the diagram to the window.
    /// Does not shrink the viewport, so no full-width chrome band appears beside a narrow floating panel.
    /// </summary>
    /// <param name="Obstacle">Region to avoid when fitting, or <see cref="Rectangle.Empty"/>.</param>
    public void SetFitObstacle(Rectangle Obstacle)
    {
        if (_FitObstacle == Obstacle)
        {
            return;
        }

        _FitObstacle = Obstacle;
        ApplyView();
    }

    /// <summary>Lays the picture out for the current zoom or fit mode.</summary>
    public void ApplyView()
    {
        if (DiagramPicture.Image == null)
        {
            DiagramPicture.Dock = DockStyle.Fill;
            DiagramPicture.SizeMode = PictureBoxSizeMode.Zoom;
            Viewport.AutoScrollMinSize = Size.Empty;
            return;
        }

        if (_FitToWindow)
        {
            Viewport.AutoScrollPosition = Point.Empty;
            Viewport.AutoScrollMinSize = Size.Empty;
            LayoutFitToWindowPicture();
        }
        else
        {
            int Width = Math.Max(1, (int)Math.Round(DiagramPicture.Image.Width * _Zoom));
            int Height = Math.Max(1, (int)Math.Round(DiagramPicture.Image.Height * _Zoom));
            DiagramPicture.Dock = DockStyle.None;
            DiagramPicture.Location = Point.Empty;
            DiagramPicture.Size = new Size(Width, Height);
            DiagramPicture.SizeMode = PictureBoxSizeMode.Zoom;
            Viewport.AutoScrollMinSize = new Size(Width, Height);
        }

        DiagramPicture.Visible = true;
        DiagramPicture.BringToFront();
        DiagramPicture.Invalidate();
        Viewport.Invalidate();
        PositionZoomButtons();
    }

    /// <summary>Zooms in around the centre of the viewport.</summary>
    public void ZoomIn()
    {
        if (!_NavigationEnabled)
        {
            return;
        }

        Point Centre = new Point(Viewport.ClientSize.Width / 2, Viewport.ClientSize.Height / 2);
        SetZoomAt(DisplayedZoom() * ZOOM_STEP, Centre);
    }

    /// <summary>Zooms out around the centre of the viewport.</summary>
    public void ZoomOut()
    {
        if (!_NavigationEnabled)
        {
            return;
        }

        Point Centre = new Point(Viewport.ClientSize.Width / 2, Viewport.ClientSize.Height / 2);
        SetZoomAt(DisplayedZoom() / ZOOM_STEP, Centre);
    }

    /// <summary>
    /// Sizes the canvas to the diagram picture at 100 percent so the full extents can be scrolled.
    /// </summary>
    public void ExpandToDiagramExtents()
    {
        if (DiagramPicture.Image == null)
        {
            return;
        }

        _FitToWindow = false;
        _Zoom = 1;
        ApplyView();
        Viewport.AutoScrollPosition = new Point(0, 0);
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Scales the picture so the whole diagram fits the viewport.</summary>
    public void Fit()
    {
        if (!_NavigationEnabled || DiagramPicture.Image == null)
        {
            return;
        }

        _FitToWindow = true;
        ApplyView();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks the picture to paint again.</summary>
    public void InvalidatePicture()
    {
        DiagramPicture.Invalidate();
    }

    /// <summary>Gives the preview keyboard focus so Esc can clear a selection.</summary>
    public void FocusForKeys()
    {
        if (FocusSink.CanFocus)
        {
            FocusSink.Focus();
        }
    }

    /// <summary>Raises the Esc action, as if the preview had focus.</summary>
    public void PressEscape()
    {
        FocusSink.HandleKey(Keys.Escape);
    }

    /// <summary>
    /// Reads the picture box size and the image size.
    /// </summary>
    /// <param name="ClientWidth">Picture box client width.</param>
    /// <param name="ClientHeight">Picture box client height.</param>
    /// <param name="ImageWidth">Image width in pixels.</param>
    /// <param name="ImageHeight">Image height in pixels.</param>
    /// <returns>False when there is no picture.</returns>
    public bool TryGetPictureMetrics(out int ClientWidth, out int ClientHeight, out int ImageWidth, out int ImageHeight)
    {
        ClientWidth = DiagramPicture.ClientSize.Width;
        ClientHeight = DiagramPicture.ClientSize.Height;
        Image? Shown = DiagramPicture.Image;
        if (Shown == null)
        {
            ImageWidth = 0;
            ImageHeight = 0;
            return false;
        }

        ImageWidth = Shown.Width;
        ImageHeight = Shown.Height;
        return true;
    }

    /// <summary>
    /// Converts a picture-client point to screen coordinates.
    /// </summary>
    /// <param name="ClientPoint">Point in the picture box.</param>
    /// <returns>The same point on the screen.</returns>
    public Point PictureToScreen(Point ClientPoint)
    {
        return DiagramPicture.PointToScreen(ClientPoint);
    }

    /// <summary>Gets the picture box bounds on the screen.</summary>
    public Rectangle PictureScreenRectangle()
    {
        return DiagramPicture.RectangleToScreen(DiagramPicture.ClientRectangle);
    }

    /// <summary>
    /// When the pointer is over the diagram, eats the wheel so the editor does not scroll or zoom.
    /// </summary>
    /// <param name="Message">A window message.</param>
    /// <returns>True when this preview used the wheel.</returns>
    public bool TryHandleMouseWheel(Message Message)
    {
        if (!_NavigationEnabled || Message.Msg != WM_MOUSEWHEEL || !IsPointerOverViewport())
        {
            return false;
        }

        int Delta = unchecked((short)((ulong)Message.WParam.ToInt64() >> 16));
        if (Delta == 0)
        {
            return false;
        }

        Point OnViewport = Viewport.PointToClient(Cursor.Position);
        HandleMouseWheel(Delta, OnViewport, null);
        return true;
    }

    private void Picture_MouseMove(object? Sender, MouseEventArgs E)
    {
        PictureMouseMove?.Invoke(this, E);
    }

    private void Picture_MouseLeave(object? Sender, EventArgs E)
    {
        PictureMouseLeave?.Invoke(this, E);
    }

    private void DiagramMenu_Opening(object? Sender, CancelEventArgs E)
    {
        ClearMenuItem.Enabled = SelectedItems != null;
        CopyPngMenuItem.Enabled = Image != null;
    }

    private void ClearMenuItem_Click(object? Sender, EventArgs E)
    {
        SelectedItems = null;
    }

    private void RefreshMenuItem_Click(object? Sender, EventArgs E)
    {
        if (RefreshRequested != null)
        {
            RefreshRequested.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_Index != null && _Index.Objects.Count > 0)
        {
            RenderPictureFromItems();
        }

        Refresh();
    }

    private void CopyPngMenuItem_Click(object? Sender, EventArgs E)
    {
        Image? Shown = Image;
        if (Shown == null)
        {
            return;
        }

        using Bitmap Copy = new Bitmap(Shown);
        using MemoryStream Stream = new MemoryStream();
        Copy.Save(Stream, ImageFormat.Png);
        Stream.Position = 0;
        using MemoryStream Png = new MemoryStream(Stream.ToArray());
        DataObject Data = new DataObject();
        Data.SetData(DataFormats.Bitmap, true, Copy);
        Data.SetData("PNG", false, Png);
        Clipboard.SetDataObject(Data, true);
    }

    private void Viewport_MouseDown(object? Sender, MouseEventArgs E)
    {
        if (E.Button != MouseButtons.Left)
        {
            return;
        }

        FocusForKeys();
        bool Extend = (ModifierKeys & Keys.Control) == Keys.Control;
        Point PicturePoint = DiagramPicture.PointToClient(Viewport.PointToScreen(E.Location));
        if (DiagramPicture.ClientRectangle.Contains(PicturePoint))
        {
            PreviewObject? Hit = null;
            if (IsOnDiagram(PicturePoint))
            {
                Hit = HitItem(PicturePoint);
            }

            ApplyClick(Hit, Extend);
            return;
        }

        ApplyClick(null, Extend);
    }

    private void Picture_MouseDown(object? Sender, MouseEventArgs E)
    {
        if (E.Button == MouseButtons.Left)
        {
            FocusForKeys();
            bool Extend = (ModifierKeys & Keys.Control) == Keys.Control;
            PreviewObject? Hit = null;
            if (IsOnDiagram(E.Location))
            {
                Hit = HitItem(E.Location);
            }

            ApplyClick(Hit, Extend);
        }

        PictureMouseDown?.Invoke(this, E);
    }

    private void Picture_Paint(object? Sender, PaintEventArgs E)
    {
        if (TryGetPictureTransform(out double ScaleX, out double ScaleY, out double OffsetX, out double OffsetY))
        {
            if (_Index != null)
            {
                foreach (PreviewObject Item in _Index.Objects)
                {
                    if (Item.Selected)
                    {
                        PreviewSelection.Paint(E.Graphics, Item, _SelectionColour, ScaleX, ScaleY, OffsetX, OffsetY);
                    }
                    else if (Item.Style.IsCustom)
                    {
                        PreviewSelection.Paint(E.Graphics, Item, Item.Style, ScaleX, ScaleY, OffsetX, OffsetY);
                    }
                }
            }
        }

        PicturePaint?.Invoke(this, E);
    }

    /// <summary>
    /// Applies a left click. A miss clears the selection. Ctrl-click toggles the item under the pointer.
    /// </summary>
    /// <param name="Hit">Item under the pointer, or null for empty space.</param>
    /// <param name="Extend">True when Ctrl is held.</param>
    internal void ApplyClick(PreviewObject? Hit, bool Extend)
    {
        if (Hit == null)
        {
            SelectedItems = null;
            return;
        }

        if (Extend)
        {
            ToggleItem(Hit);
            return;
        }

        SelectedItems = new PreviewObject[] { Hit };
    }

    /// <summary>
    /// Selects the item, or deselects it when it is already selected. Other selected items stay selected.
    /// </summary>
    /// <param name="Item">Item under the pointer.</param>
    internal void ToggleItem(PreviewObject Item)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (ContainsReference(_SelectedItems, Item))
        {
            Item.Selected = false;
            _SelectedItems.Remove(Item);
        }
        else
        {
            Item.Selected = true;
            _SelectedItems.Add(Item);
        }

        DiagramPicture.Invalidate();
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AssignSelectedItems(IReadOnlyList<PreviewObject>? Next)
    {
        List<PreviewObject> Desired = new List<PreviewObject>();
        if (Next != null)
        {
            foreach (PreviewObject Item in Next)
            {
                if (Item == null)
                {
                    throw new ArgumentException("The selection contains a null item.", nameof(Next));
                }

                if (!ContainsReference(Desired, Item))
                {
                    Desired.Add(Item);
                }
            }
        }

        if (SameItems(Desired))
        {
            foreach (PreviewObject Item in Desired)
            {
                Item.Selected = true;
            }

            return;
        }

        ClearSelectedFlags(Desired);
        _SelectedItems.Clear();
        _SelectedItems.AddRange(Desired);
        foreach (PreviewObject Item in _SelectedItems)
        {
            Item.Selected = true;
        }

        DiagramPicture.Invalidate();
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSelectedFlags(IReadOnlyList<PreviewObject> Keep)
    {
        if (_Index != null)
        {
            foreach (PreviewObject Item in _Index.Objects)
            {
                if (Item.Selected && !ContainsReference(Keep, Item))
                {
                    Item.Selected = false;
                }
            }
        }

        foreach (PreviewObject Item in _SelectedItems)
        {
            if (Item.Selected && !ContainsReference(Keep, Item))
            {
                Item.Selected = false;
            }
        }
    }

    private bool SameItems(List<PreviewObject> Desired)
    {
        if (Desired.Count != _SelectedItems.Count)
        {
            return false;
        }

        for (int Index = 0; Index < Desired.Count; Index++)
        {
            if (!ReferenceEquals(Desired[Index], _SelectedItems[Index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsReference(IReadOnlyList<PreviewObject> Items, PreviewObject Candidate)
    {
        for (int Index = 0; Index < Items.Count; Index++)
        {
            if (ReferenceEquals(Items[Index], Candidate))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetItemViewportBounds(PreviewObject Item, out Rectangle Bounds)
    {
        Bounds = Rectangle.Empty;
        if (!TryGetPictureTransform(out double ScaleX, out double ScaleY, out double OffsetX, out double OffsetY))
        {
            return false;
        }

        if (!TryGetSvgBounds(Item, out double Left, out double Top, out double Right, out double Bottom))
        {
            return false;
        }

        double X1 = OffsetX + (Left * ScaleX);
        double Y1 = OffsetY + (Top * ScaleY);
        double X2 = OffsetX + (Right * ScaleX);
        double Y2 = OffsetY + (Bottom * ScaleY);
        int ViewLeft = DiagramPicture.Left + (int)Math.Floor(Math.Min(X1, X2));
        int ViewTop = DiagramPicture.Top + (int)Math.Floor(Math.Min(Y1, Y2));
        int ViewRight = DiagramPicture.Left + (int)Math.Ceiling(Math.Max(X1, X2));
        int ViewBottom = DiagramPicture.Top + (int)Math.Ceiling(Math.Max(Y1, Y2));
        if (ViewRight <= ViewLeft)
        {
            ViewRight = ViewLeft + 1;
        }

        if (ViewBottom <= ViewTop)
        {
            ViewBottom = ViewTop + 1;
        }

        Bounds = Rectangle.FromLTRB(ViewLeft, ViewTop, ViewRight, ViewBottom);
        return true;
    }

    private static bool TryGetSvgBounds(PreviewObject Item, out double Left, out double Top, out double Right, out double Bottom)
    {
        Left = Item.Left;
        Top = Item.Top;
        Right = Item.Right;
        Bottom = Item.Bottom;
        if (Right > Left && Bottom > Top)
        {
            return true;
        }

        Left = double.MaxValue;
        Top = double.MaxValue;
        Right = double.MinValue;
        Bottom = double.MinValue;
        bool Any = false;
        foreach (PreviewOutline Outline in Item.Outlines)
        {
            foreach (PointF Point in Outline.Points)
            {
                Any = true;
                Left = Math.Min(Left, Point.X);
                Top = Math.Min(Top, Point.Y);
                Right = Math.Max(Right, Point.X);
                Bottom = Math.Max(Bottom, Point.Y);
            }
        }

        return Any && Right > Left && Bottom > Top;
    }

    private bool TryGetPictureTransform(out double ScaleX, out double ScaleY, out double OffsetX, out double OffsetY)
    {
        ScaleX = 0;
        ScaleY = 0;
        OffsetX = 0;
        OffsetY = 0;
        Image? Shown = DiagramPicture.Image;
        if (Shown == null || _Index == null)
        {
            return false;
        }

        return PreviewSelection.TryGetSvgToClient(
            DiagramPicture.ClientSize.Width,
            DiagramPicture.ClientSize.Height,
            Shown.Width,
            Shown.Height,
            _Index.Width,
            _Index.Height,
            out ScaleX,
            out ScaleY,
            out OffsetX,
            out OffsetY);
    }

    private void FocusSink_EscapePressed(object? Sender, EventArgs E)
    {
        EscapePressed?.Invoke(this, E);
    }

    private void FocusSink_LineStepPressed(object? Sender, KeyEventArgs E)
    {
        LineStepRequested?.Invoke(this, E);
    }

    private void ZoomInButton_Click(object? Sender, EventArgs E)
    {
        ZoomIn();
    }

    private void ZoomOutButton_Click(object? Sender, EventArgs E)
    {
        ZoomOut();
    }

    private void FitButton_Click(object? Sender, EventArgs E)
    {
        Fit();
    }

    private void FitButton_Paint(object? Sender, PaintEventArgs E)
    {
        Rectangle Box = new Rectangle(8, 8, 14, 14);
        using Pen Outline = new Pen(FitButton.ForeColor, 1.6F);
        E.Graphics.DrawRectangle(Outline, Box);
    }

    private void Viewport_Resize(object? Sender, EventArgs E)
    {
        if (DiagramPicture.Image != null)
        {
            ApplyView();
        }
    }

    private void PreviewCanvas_Layout(object? Sender, LayoutEventArgs E)
    {
        LayoutViewport();
        PositionZoomButtons();
    }

    private void LayoutViewport()
    {
        int BannerHeight = Banner.Visible ? Banner.Height : 0;
        Rectangle Client = ClientRectangle;
        if (Viewport.Dock != DockStyle.None)
        {
            Viewport.Dock = DockStyle.None;
        }

        Viewport.SetBounds(Client.Left, Client.Top + BannerHeight, Client.Width, Math.Max(1, Client.Height - BannerHeight));
    }

    private Size GetFitAreaSize()
    {
        Size ViewSize = Viewport.ClientSize;
        int FitWidth = Math.Max(1, ViewSize.Width);
        int FitHeight = Math.Max(1, ViewSize.Height);
        if (!_FitObstacle.IsEmpty && _FitObstacle.Top > Viewport.Top)
        {
            FitHeight = Math.Max(1, _FitObstacle.Top - Viewport.Top);
        }

        return new Size(FitWidth, FitHeight);
    }

    private void LayoutFitToWindowPicture()
    {
        Image? Shown = DiagramPicture.Image;
        if (Shown == null)
        {
            return;
        }

        Size FitArea = GetFitAreaSize();
        double Scale = Math.Min(FitArea.Width / (double)Shown.Width, FitArea.Height / (double)Shown.Height);
        int DisplayWidth = Math.Max(1, (int)Math.Round(Shown.Width * Scale));
        int DisplayHeight = Math.Max(1, (int)Math.Round(Shown.Height * Scale));
        DiagramPicture.Dock = DockStyle.None;
        DiagramPicture.SizeMode = PictureBoxSizeMode.StretchImage;
        DiagramPicture.Size = new Size(DisplayWidth, DisplayHeight);
        DiagramPicture.Location = new Point((FitArea.Width - DisplayWidth) / 2, (FitArea.Height - DisplayHeight) / 2);
    }

    private void Viewport_MouseWheel(object? Sender, MouseEventArgs E)
    {
        HandleMouseWheel(E.Delta, E.Location, E);
    }

    private void Picture_MouseWheel(object? Sender, MouseEventArgs E)
    {
        Point OnViewport = Viewport.PointToClient(DiagramPicture.PointToScreen(E.Location));
        HandleMouseWheel(E.Delta, OnViewport, E);
    }

    private bool IsPointerOverViewport()
    {
        if (Viewport.IsDisposed || !Viewport.Visible)
        {
            return false;
        }

        Rectangle Area = Viewport.RectangleToScreen(Viewport.ClientRectangle);
        return Area.Contains(Cursor.Position);
    }

    private void HandleMouseWheel(int Delta, Point ViewportClient, MouseEventArgs? E)
    {
        if (!_NavigationEnabled)
        {
            return;
        }

        Keys Mods = ModifierKeys;
        if ((Mods & Keys.Control) == Keys.Control)
        {
            if (E is HandledMouseEventArgs Handled)
            {
                Handled.Handled = true;
            }

            double NextZoom = Delta > 0 ? DisplayedZoom() * ZOOM_STEP : DisplayedZoom() / ZOOM_STEP;
            SetZoomAt(NextZoom, ViewportClient);
            return;
        }

        if (E is HandledMouseEventArgs Marked)
        {
            Marked.Handled = true;
        }

        if ((Mods & Keys.Shift) == Keys.Shift)
        {
            ScrollByWheel(Delta, Horizontal: true);
            return;
        }

        ScrollByWheel(Delta, Horizontal: false);
    }

    private void ScrollByWheel(int Delta, bool Horizontal)
    {
        int ScrollX = -Viewport.AutoScrollPosition.X;
        int ScrollY = -Viewport.AutoScrollPosition.Y;
        int Notch = SystemInformation.MouseWheelScrollDelta;
        if (Notch <= 0)
        {
            Notch = 120;
        }

        int LinePixels = Math.Max(16, SystemInformation.MouseWheelScrollLines * 16);
        if (SystemInformation.MouseWheelScrollLines < 0)
        {
            int Span = Horizontal ? Viewport.ClientSize.Width : Viewport.ClientSize.Height;
            LinePixels = Math.Max(16, Span / 4);
        }

        int DeltaNotches = Delta / Notch;
        if (DeltaNotches == 0 && Delta != 0)
        {
            DeltaNotches = Math.Sign(Delta);
        }

        int Step = DeltaNotches * LinePixels;
        if (Horizontal)
        {
            int NewX = Math.Max(0, ScrollX - Step);
            Viewport.AutoScrollPosition = new Point(NewX, ScrollY);
            return;
        }

        int NewY = Math.Max(0, ScrollY - Step);
        Viewport.AutoScrollPosition = new Point(ScrollX, NewY);
    }

    private void SetZoomAt(double RequestedZoom, Point ClientPoint)
    {
        double NewZoom = ClampZoom(RequestedZoom);
        if (DiagramPicture.Image == null)
        {
            _Zoom = NewZoom;
            _FitToWindow = false;
            ApplyView();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        double OldZoom = DisplayedZoom();
        if (Math.Abs(NewZoom - OldZoom) < 0.0001 && !_FitToWindow)
        {
            return;
        }

        GetImagePointUnderClient(ClientPoint, out double ImageX, out double ImageY);
        _Zoom = NewZoom;
        _FitToWindow = false;
        ApplyView();
        int ContentX = (int)Math.Round(ImageX * NewZoom);
        int ContentY = (int)Math.Round(ImageY * NewZoom);
        Viewport.AutoScrollPosition = new Point(Math.Max(0, ContentX - ClientPoint.X), Math.Max(0, ContentY - ClientPoint.Y));
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private void GetImagePointUnderClient(Point ClientPoint, out double ImageX, out double ImageY)
    {
        Image Shown = DiagramPicture.Image!;
        if (_FitToWindow)
        {
            Size Area = GetFitAreaSize();
            double Fitted = Math.Min(Area.Width / (double)Shown.Width, Area.Height / (double)Shown.Height);
            Fitted = Math.Max(0.0001, Fitted);
            int DisplayWidth = Math.Max(1, (int)Math.Round(Shown.Width * Fitted));
            int DisplayHeight = Math.Max(1, (int)Math.Round(Shown.Height * Fitted));
            int OffsetX = (Area.Width - DisplayWidth) / 2;
            int OffsetY = (Area.Height - DisplayHeight) / 2;
            ImageX = (ClientPoint.X - OffsetX) / Fitted;
            ImageY = (ClientPoint.Y - OffsetY) / Fitted;
            return;
        }

        Point Scroll = Viewport.AutoScrollPosition;
        double Scale = Math.Max(0.0001, _Zoom);
        ImageX = (ClientPoint.X - Scroll.X) / Scale;
        ImageY = (ClientPoint.Y - Scroll.Y) / Scale;
    }

    private double DisplayedZoom()
    {
        if (!_FitToWindow || DiagramPicture.Image == null)
        {
            return _Zoom;
        }

        int ImageWidth = DiagramPicture.Image.Width;
        int ImageHeight = DiagramPicture.Image.Height;
        Size Area = GetFitAreaSize();
        if (ImageWidth <= 0 || ImageHeight <= 0 || Area.Width <= 0 || Area.Height <= 0)
        {
            return _Zoom;
        }

        double Fitted = Math.Min(Area.Width / (double)ImageWidth, Area.Height / (double)ImageHeight);
        return ClampZoom(Fitted);
    }

    private static double ClampZoom(double Zoom)
    {
        return Math.Max(MINIMUM_ZOOM, Math.Min(MAXIMUM_ZOOM, Zoom));
    }

    /// <summary>
    /// Keeps zoom controls above the diagram and any floating overlays on the preview.
    /// </summary>
    internal void EnsureZoomButtonsOnTop()
    {
        PositionZoomButtons();
    }

    private void PositionZoomButtons()
    {
        if (_PositioningZoomButtons)
        {
            return;
        }

        _PositioningZoomButtons = true;
        try
        {
            const int BUTTON = 32;
            const int GAP = 4;
            const int MARGIN = 10;
            int Right = Viewport.Left + Viewport.ClientSize.Width;
            int Bottom = Viewport.Top + Viewport.ClientSize.Height;
            int X = Math.Max(Viewport.Left, Right - MARGIN - BUTTON);
            int FitTop = Math.Max(Viewport.Top, Bottom - MARGIN - BUTTON);
            int OutTop = Math.Max(Viewport.Top, FitTop - GAP - BUTTON);
            int InTop = Math.Max(Viewport.Top, OutTop - GAP - BUTTON);
            ZoomInButton.SetBounds(X, InTop, BUTTON, BUTTON);
            ZoomOutButton.SetBounds(X, OutTop, BUTTON, BUTTON);
            FitButton.SetBounds(X, FitTop, BUTTON, BUTTON);
            ZoomInButton.BringToFront();
            ZoomOutButton.BringToFront();
            FitButton.BringToFront();
        }
        finally
        {
            _PositioningZoomButtons = false;
        }
    }
}
