using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Properties for the selected diagram object, under the preview. It shows the picture, the description,
/// editor line, connection facts, and a type change.
/// </summary>
internal sealed partial class SelectionPanel : UserControl
{
    private const int CHANGE_TYPE_BUTTON_MIN_WIDTH = 75;

    private const int CHANGE_TYPE_BUTTON_MIN_HEIGHT = 23;

    private Image? _OwnedPicture;

    private bool _Filling;

    /// <summary>
    /// Creates the panel.
    /// </summary>
    public SelectionPanel()
    {
        InitializeComponent();
        LayoutDetailRows();
        ShowNone();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        LayoutDetailRows();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutDetailRows();
    }

    private void SelectionPanel_Load(object? Sender, EventArgs e)
    {
        LayoutDetailRows();
    }

    private void LayoutDetailRows()
    {
        LayoutTypeRow();
        LayoutEditorLineRow();
    }

    private void LayoutTypeRow()
    {
        Size ButtonPreferred = ChangeTypeButton.PreferredSize;
        int ButtonWidth = Math.Max(CHANGE_TYPE_BUTTON_MIN_WIDTH, ButtonPreferred.Width);
        int ButtonHeight = Math.Max(CHANGE_TYPE_BUTTON_MIN_HEIGHT, ButtonPreferred.Height);
        ChangeTypeButton.Size = new Size(ButtonWidth, ButtonHeight);
        int ComboHeight = Math.Max(CHANGE_TYPE_BUTTON_MIN_HEIGHT, TypeCombo.PreferredSize.Height);
        TypeCombo.Height = ComboHeight;
        int LabelHeight = TypeLabel.Height;
        int ContentHeight = Math.Max(ButtonHeight, Math.Max(ComboHeight, LabelHeight));
        int TypeRowHeight = Math.Max(40, ContentHeight + 8);
        TypeRow.RowStyles[0] = new RowStyle(SizeType.Absolute, TypeRowHeight);
        DetailTable.RowStyles[4] = new RowStyle(SizeType.Absolute, TypeRowHeight);
        int PadTop = (TypeRowHeight - ContentHeight) / 2;
        TypeLabel.Margin = new Padding(4, PadTop + (ContentHeight - LabelHeight) / 2, 8, 0);
        TypeCombo.Margin = new Padding(4, PadTop + (ContentHeight - ComboHeight) / 2, 8, 0);
        ChangeTypeButton.Margin = new Padding(0, PadTop + (ContentHeight - ButtonHeight) / 2, 4, 0);
        ChangeTypeButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
    }

    private void LayoutEditorLineRow()
    {
        int RowHeight = (int)EditorLineRow.RowStyles[0].Height;
        int ValueHeight = EditorLineValue.Height;
        int CaptionHeight = EditorLineCaption.Height;
        int ContentHeight = Math.Max(ValueHeight, CaptionHeight);
        int PadTop = (RowHeight - ContentHeight) / 2;
        EditorLineCaption.Margin = new Padding(4, PadTop + (ContentHeight - CaptionHeight) / 2, 8, 0);
        EditorLineValue.Margin = new Padding(4, PadTop + (ContentHeight - ValueHeight) / 2, 0, 0);
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
            DescriptionBox.Text = string.Empty;
            SetEditorLine(0);
            FactsBox.Text = string.Empty;
            SetPicture(null);
            TypeRow.Visible = false;
            Visible = false;
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
                List<TypeChoice> Choices = new List<TypeChoice>();
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
