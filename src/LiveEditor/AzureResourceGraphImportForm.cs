using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LiveEditor.Azure;

namespace LiveEditor;

/// <summary>
/// Chooses which Azure resource categories to import into an enterprise diagram.
/// </summary>
internal sealed partial class AzureResourceGraphImportForm : Form
{
    private AzureResourceGraphImportOptions _Options;

    private Dictionary<string, int> _RowCountsByCategory = new Dictionary<string, int>(StringComparer.Ordinal);

    private bool _UseEnterpriseDefaultChecks;

    private bool _SuppressCategoryListEvents;

    public AzureResourceGraphImportForm()
    {
        InitializeComponent();
        _Options = AzureResourceGraphImportOptions.CreateEnterpriseDefaults();
    }

    /// <summary>Gets the options chosen when the dialog returns OK.</summary>
    public AzureResourceGraphImportOptions Options
    {
        get
        {
            return _Options;
        }
    }

    /// <summary>
    /// Loads row counts from a CSV file and shows the dialog.
    /// </summary>
    public static bool TryShow(IWin32Window Owner, string CsvFilePath, out AzureResourceGraphImportOptions? Options)
    {
        Options = null;
        using AzureResourceGraphImportForm Form = new AzureResourceGraphImportForm();
        Form.InitialiseFromFile(CsvFilePath);
        if (Form.ShowDialog(Owner) != DialogResult.OK)
        {
            return false;
        }

        Options = Form.Options;
        return true;
    }

    /// <summary>Shows the same category choices for resources retrieved directly from Azure.</summary>
    public static bool TryShow(IWin32Window Owner, IReadOnlyList<AzureResourceGraphRecord> Records,
        string Title, out AzureResourceGraphImportOptions? Options)
    {
        Options = null;
        using AzureResourceGraphImportForm Form = new AzureResourceGraphImportForm();
        Form.TitleTextBox.Text = Title;
        Form.InitialiseFromRecords(Records);
        if (Form.ShowDialog(Owner) != DialogResult.OK)
        {
            return false;
        }

        Options = Form.Options;
        return true;
    }

    private void InitialiseFromFile(string CsvFilePath)
    {
        string FileName = System.IO.Path.GetFileNameWithoutExtension(CsvFilePath);
        if (!string.IsNullOrWhiteSpace(FileName))
        {
            TitleTextBox.Text = "Azure: " + FileName;
        }

        _RowCountsByCategory = new Dictionary<string, int>(StringComparer.Ordinal);
        if (AzureResourceGraphReader.TryReadFile(CsvFilePath, out List<AzureResourceGraphRecord> Records, out _))
        {
            InitialiseFromRecords(Records);
            return;
        }
        else
        {
            SummaryLabel.Text = "Choose which resource types to include in the enterprise diagram.";
        }

        _UseEnterpriseDefaultChecks = true;
        PopulateCategoryList();
    }

    private void InitialiseFromRecords(IReadOnlyList<AzureResourceGraphRecord> Records)
    {
        _RowCountsByCategory = AzureResourceGraphIncludeCatalog.CountByCategory(Records);
        int Selected = CountRowsForOptions(Records, AzureResourceGraphImportOptions.CreateEnterpriseDefaults());
        SummaryLabel.Text = $"Found {Records.Count} resources. Enterprise defaults import about {Selected} of them (disks, NICs, alerts, and similar types are excluded).";
        _UseEnterpriseDefaultChecks = true;
        PopulateCategoryList();
    }

    private void PopulateCategoryList()
    {
        HashSet<string> PreviouslyChecked = new HashSet<string>(StringComparer.Ordinal);
        for (int Index = 0; Index < CategoryList.Items.Count; Index++)
        {
            AzureResourceGraphCategoryListItem? Existing = CategoryList.Items[Index] as AzureResourceGraphCategoryListItem;
            if (Existing != null && CategoryList.GetItemChecked(Index))
            {
                PreviouslyChecked.Add(Existing.Definition.Key);
            }
        }

        _SuppressCategoryListEvents = true;
        try
        {
            CategoryList.Items.Clear();
            foreach (AzureResourceGraphIncludeCategoryDefinition Definition in AzureResourceGraphIncludeCatalog.GetDialogDefinitions(ShowInfrastructureDetailCheckBox.Checked))
            {
                int Count = 0;
                if (_RowCountsByCategory.ContainsKey(Definition.Key))
                {
                    Count = _RowCountsByCategory[Definition.Key];
                }

                AzureResourceGraphCategoryListItem Item = new AzureResourceGraphCategoryListItem(Definition, Count);
                int Index = CategoryList.Items.Add(Item);
                bool Checked = _UseEnterpriseDefaultChecks
                    ? Definition.DefaultEnabledForEnterprise
                    : PreviouslyChecked.Contains(Definition.Key);
                CategoryList.SetItemChecked(Index, Checked);
            }
        }
        finally
        {
            _SuppressCategoryListEvents = false;
        }

        _UseEnterpriseDefaultChecks = false;
        UpdateSelectedCountLabel();
    }

    private void ShowInfrastructureDetailCheckBox_CheckedChanged(object? Sender, EventArgs E)
    {
        PopulateCategoryList();
    }

    private void EnterpriseDefaultsButton_Click(object? Sender, EventArgs E)
    {
        ShowInfrastructureDetailCheckBox.Checked = false;
        _UseEnterpriseDefaultChecks = true;
        PopulateCategoryList();
    }

    private void SelectAllButton_Click(object? Sender, EventArgs E)
    {
        SetAllCategoryChecks(true);
    }

    private void SelectNoneButton_Click(object? Sender, EventArgs E)
    {
        SetAllCategoryChecks(false);
    }

    private void SetAllCategoryChecks(bool Checked)
    {
        _SuppressCategoryListEvents = true;
        try
        {
            for (int Index = 0; Index < CategoryList.Items.Count; Index++)
            {
                CategoryList.SetItemChecked(Index, Checked);
            }
        }
        finally
        {
            _SuppressCategoryListEvents = false;
        }

        UpdateSelectedCountLabel();
    }

    private void CategoryList_ItemCheck(object? Sender, ItemCheckEventArgs E)
    {
        if (_SuppressCategoryListEvents)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(UpdateSelectedCountLabel);
    }

    private void OkButton_Click(object? Sender, EventArgs E)
    {
        AzureResourceGraphImportOptions Built = new AzureResourceGraphImportOptions
        {
            ModelTitle = TitleTextBox.Text.Trim(),
            GroupBySubscription = GroupBySubscriptionCheckBox.Checked,
            LinkNestedResources = LinkNestedResourcesCheckBox.Checked,
            LinkHiddenTagReferences = LinkHiddenTagsCheckBox.Checked
        };

        if (string.IsNullOrWhiteSpace(Built.ModelTitle))
        {
            Built.ModelTitle = "Azure resources";
        }

        for (int Index = 0; Index < CategoryList.Items.Count; Index++)
        {
            if (!CategoryList.GetItemChecked(Index))
            {
                continue;
            }

            AzureResourceGraphCategoryListItem? Item = CategoryList.Items[Index] as AzureResourceGraphCategoryListItem;
            if (Item != null)
            {
                Built.EnabledCategories.Add(Item.Definition.Key);
            }
        }

        if (Built.EnabledCategories.Count == 0)
        {
            MessageBox.Show(this, "Select at least one resource category to import.", "Import Azure resources", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.None;
            return;
        }

        _Options = Built;
        DialogResult = DialogResult.OK;
    }

    private void UpdateSelectedCountLabel()
    {
        int Checked = 0;
        int Rows = 0;
        for (int Index = 0; Index < CategoryList.Items.Count; Index++)
        {
            AzureResourceGraphCategoryListItem? Item = CategoryList.Items[Index] as AzureResourceGraphCategoryListItem;
            if (Item == null)
            {
                continue;
            }

            if (CategoryList.GetItemChecked(Index))
            {
                Checked++;
                Rows += Item.RowCount;
            }
        }

        string CountText = $"{Checked.ToString()} categories selected ({Rows.ToString()} resources).";
        if (Rows >= 80)
        {
            CountText += " Layout can take a minute; the preview scales very wide diagrams to fit.";
        }

        SelectedCountLabel.Text = CountText;
    }

    private static int CountRowsForOptions(IReadOnlyList<AzureResourceGraphRecord> Records, AzureResourceGraphImportOptions Options)
    {
        int Count = 0;
        foreach (AzureResourceGraphRecord Record in Records)
        {
            if (AzureResourceGraphIncludeCatalog.IsTypeIncluded(Record.Type, Options))
            {
                Count++;
            }
        }

        return Count;
    }

    private sealed class AzureResourceGraphCategoryListItem
    {
        public AzureResourceGraphCategoryListItem(AzureResourceGraphIncludeCategoryDefinition Definition, int RowCount)
        {
            this.Definition = Definition;
            this.RowCount = RowCount;
        }

        public AzureResourceGraphIncludeCategoryDefinition Definition { get; }

        public int RowCount { get; }

        public override string ToString()
        {
            return Definition.DisplayName + " (" + RowCount.ToString() + ")";
        }
    }
}
