using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Properties for the selected diagram object. It shows the picture, the description,
/// editor line, connection facts, and a type change. Floated over the bottom-left of the diagram preview.
/// </summary>
internal sealed partial class SelectionPanel : UserControl
{
    /// <summary>Height that fits every row in <see cref="DetailTable"/> plus panel padding.</summary>
    internal int PreferredFloatingHeight
    {
        get
        {
            int RowTotal = 0;
            for (int Index = 0; Index < DetailTable.RowCount; Index++)
            {
                RowStyle Style = DetailTable.RowStyles[Index];
                if (Style.SizeType == SizeType.Absolute)
                {
                    RowTotal += (int)Math.Ceiling(Style.Height);
                }
            }

            int Height = RowTotal + Padding.Vertical + 4;
            return Math.Max(MinimumSize.Height, Height);
        }
    }

    private Image? _OwnedPicture;

    private bool _Filling;

    /// <summary>
    /// Creates the panel.
    /// </summary>
    public SelectionPanel()
    {
        InitializeComponent();
        ShowNone();
    }

    /// <summary>
    /// Raised when Change type is clicked.
    /// </summary>
    public event EventHandler? ChangeTypeClicked;

    /// <summary>Gets the type token chosen in the list.</summary>
    public string SelectedKebab
    {
        get
        {
            if (TypeCombo.SelectedItem is TypeChoice Choice)
            {
                return Choice.Kebab;
            }

            return string.Empty;
        }
    }

    /// <summary>Gets whether relationships the new type does not allow should be removed.</summary>
    public bool RemoveDisallowedRelationships
    {
        get
        {
            return UpdateRelationshipsRadio.Checked;
        }
    }

    /// <summary>
    /// Shows the empty prompt.
    /// </summary>
    public void ShowNone()
    {
        _Filling = true;
        try
        {
            TitleLabel.Text = "Selection";
            DescriptionBox.Text = "Nothing selected. Click a shape in the preview or place the caret on a diagram line.";
            SetEditorLine(0);
            FactsBox.Text = string.Empty;
            SetPicture(null);
            TypeRow.Visible = false;
            Visible = true;
        }
        finally
        {
            _Filling = false;
        }
    }

    /// <summary>
    /// Shows one selected object.
    /// </summary>
    /// <param name="Title">Name shown above the description.</param>
    /// <param name="Description">Full paragraph.</param>
    /// <param name="Picture">Crop of the object. The panel takes ownership.</param>
    /// <param name="EditorLine">1-based source line, or 0 when unknown.</param>
    /// <param name="Facts">Endpoints for a relationship, or id and connection-point summary for an element.</param>
    /// <param name="CurrentKebab">Current type token, or empty when the object is not an element.</param>
    public void ShowObject(string Title, string Description, Image? Picture, int EditorLine, string Facts, string CurrentKebab)
    {
        _Filling = true;
        try
        {
            TitleLabel.Text = Title;
            DescriptionBox.Text = Description;
            SetEditorLine(EditorLine);
            FactsBox.Text = Facts;
            SetPicture(Picture);
            bool CanChange = CurrentKebab.Length > 0;
            TypeRow.Visible = CanChange;
            Visible = true;
            if (!CanChange)
            {
                return;
            }

            if (TypeCombo.Items.Count == 0)
            {
                List<TypeChoice> Choices = [];
                foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
                {
                    if (Metadata.ElementType == EnterpriseElementType.Junction)
                    {
                        continue;
                    }

                    TypeChoice Choice = new TypeChoice
                    {
                        Kebab = Metadata.KebabName,
                        Caption = ElementShapePainter.DisplayName(Metadata.KebabName)
                    };
                    Choices.Add(Choice);
                }

                TypeChoice AndJunction = new TypeChoice
                {
                    Kebab = "and-junction",
                    Caption = "And junction"
                };
                Choices.Add(AndJunction);
                TypeChoice OrJunction = new TypeChoice
                {
                    Kebab = "or-junction",
                    Caption = "Or junction"
                };
                Choices.Add(OrJunction);
                Choices.Sort(CompareChoices);
                TypeCombo.Items.AddRange(Choices.ToArray());
            }

            for (int Index = 0; Index < TypeCombo.Items.Count; Index++)
            {
                if (TypeCombo.Items[Index] is TypeChoice Choice && string.Equals(Choice.Kebab, CurrentKebab, StringComparison.Ordinal))
                {
                    TypeCombo.SelectedIndex = Index;
                    break;
                }
            }
        }
        finally
        {
            _Filling = false;
        }
    }

    private void ChangeTypeButton_Click(object? Sender, EventArgs E)
    {
        if (_Filling)
        {
            return;
        }

        ChangeTypeClicked?.Invoke(this, EventArgs.Empty);
    }

    private void SetEditorLine(int EditorLine)
    {
        if (EditorLine > 0)
        {
            int Shown = Math.Min(EditorLine, 9999);
            EditorLineValue.Text = Shown.ToString(CultureInfo.InvariantCulture);
            return;
        }

        EditorLineValue.Text = string.Empty;
    }

    private void SetPicture(Image? Picture)
    {
        Image? Previous = _OwnedPicture;
        _OwnedPicture = Picture;
        ObjectPicture.Image = Picture;
        if (Previous != null && !ReferenceEquals(Previous, Picture))
        {
            Previous.Dispose();
        }
    }

    private static int CompareChoices(TypeChoice Left, TypeChoice Right)
    {
        return string.Compare(Left.Caption, Right.Caption, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message M)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;
        const int HTTRANSPARENT = -1;
        if (M.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref M);
            if ((int)M.Result == HTCLIENT)
            {
                Point Screen = DecodeScreenPoint(M.LParam);
                Point Client = PointToClient(Screen);
                if (!ShouldCapturePointer(Client))
                {
                    M.Result = (IntPtr)HTTRANSPARENT;
                }
            }

            return;
        }

        base.WndProc(ref M);
    }

    private static Point DecodeScreenPoint(IntPtr LParam)
    {
        int Packed = LParam.ToInt32();
        return new Point((short)(Packed & 0xFFFF), (short)((Packed >> 16) & 0xFFFF));
    }

    private bool ShouldCapturePointer(Point Client)
    {
        if (!Visible)
        {
            return false;
        }

        if (HitsControl(TitleLabel, Client)
            || HitsControl(ObjectPicture, Client)
            || HitsControl(DescriptionBox, Client)
            || HitsControl(EditorLineRow, Client)
            || HitsControl(EditorLineCaption, Client)
            || HitsControl(EditorLineValue, Client)
            || HitsControl(FactsBox, Client)
            || HitsControl(RadiosRow, Client)
            || HitsControl(KeepRelationshipsRadio, Client)
            || HitsControl(UpdateRelationshipsRadio, Client))
        {
            return true;
        }

        if (!TypeRow.Visible)
        {
            return false;
        }

        return HitsControl(TypeRow, Client) || HitsControl(TypeLabel, Client) || HitsControl(TypeCombo, Client) || HitsControl(ChangeTypeButton, Client);
    }

    private bool HitsControl(Control Target, Point Client)
    {
        if (!Target.Visible)
        {
            return false;
        }

        Rectangle Bounds = RectangleToClient(Target.RectangleToScreen(Target.ClientRectangle));
        return Bounds.Contains(Client);
    }

    private sealed class TypeChoice
    {
        public string Kebab = string.Empty;

        public string Caption = string.Empty;

        public override string ToString()
        {
            return Caption;
        }
    }
}
