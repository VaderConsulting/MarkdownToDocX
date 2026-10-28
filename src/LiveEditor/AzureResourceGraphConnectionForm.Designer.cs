using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor;

partial class AzureResourceGraphConnectionForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _Operation?.Cancel();
            _Http.Dispose();
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        ConnectionLayout = new TableLayoutPanel();
        TenantLabel = new Label();
        _Tenant = new TextBox();
        ClientIdLabel = new Label();
        _ClientId = new TextBox();
        SetupHelpLabel = new Label();
        SignInFlow = new FlowLayoutPanel();
        _Connect = new Button();
        SetupLink = new LinkLabel();
        SubscriptionsLabel = new Label();
        _Subscriptions = new CheckedListBox();
        SelectionFlow = new FlowLayoutPanel();
        _All = new Button();
        _None = new Button();
        AccessLabel = new Label();
        _Status = new Label();
        ActionsFlow = new FlowLayoutPanel();
        _Cancel = new Button();
        _Import = new Button();
        _Progress = new ProgressBar();
        ConnectionLayout.SuspendLayout();
        SignInFlow.SuspendLayout();
        SelectionFlow.SuspendLayout();
        ActionsFlow.SuspendLayout();
        SuspendLayout();
        //
        // ConnectionLayout
        //
        ConnectionLayout.AutoSize = true;
        ConnectionLayout.ColumnCount = 2;
        ConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        ConnectionLayout.Controls.Add(TenantLabel, 0, 0);
        ConnectionLayout.Controls.Add(_Tenant, 1, 0);
        ConnectionLayout.Controls.Add(ClientIdLabel, 0, 1);
        ConnectionLayout.Controls.Add(_ClientId, 1, 1);
        ConnectionLayout.Controls.Add(SetupHelpLabel, 0, 2);
        ConnectionLayout.Controls.Add(SignInFlow, 0, 3);
        ConnectionLayout.Controls.Add(SubscriptionsLabel, 0, 4);
        ConnectionLayout.Controls.Add(_Subscriptions, 0, 5);
        ConnectionLayout.Controls.Add(SelectionFlow, 0, 6);
        ConnectionLayout.Controls.Add(AccessLabel, 0, 7);
        ConnectionLayout.Controls.Add(_Status, 0, 8);
        ConnectionLayout.Controls.Add(ActionsFlow, 0, 9);
        ConnectionLayout.Dock = DockStyle.Fill;
        ConnectionLayout.Location = new Point(0, 0);
        ConnectionLayout.Name = "ConnectionLayout";
        ConnectionLayout.Padding = new Padding(16);
        ConnectionLayout.RowCount = 10;
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ConnectionLayout.Size = new Size(760, 536);
        ConnectionLayout.TabIndex = 0;
        //
        // TenantLabel
        //
        TenantLabel.Anchor = AnchorStyles.Left;
        TenantLabel.AutoSize = true;
        TenantLabel.Name = "TenantLabel";
        TenantLabel.TabIndex = 0;
        TenantLabel.Text = "Tenant ID";
        //
        // _Tenant
        //
        _Tenant.Dock = DockStyle.Fill;
        _Tenant.Name = "TenantTextBox";
        _Tenant.TabIndex = 1;
        _Tenant.TextChanged += Identity_TextChanged;
        //
        // ClientIdLabel
        //
        ClientIdLabel.Anchor = AnchorStyles.Left;
        ClientIdLabel.AutoSize = true;
        ClientIdLabel.Name = "ClientIdLabel";
        ClientIdLabel.TabIndex = 2;
        ClientIdLabel.Text = "Application (client) ID";
        //
        // _ClientId
        //
        _ClientId.Dock = DockStyle.Fill;
        _ClientId.Name = "ClientIdTextBox";
        _ClientId.TabIndex = 3;
        _ClientId.TextChanged += Identity_TextChanged;
        //
        // SetupHelpLabel
        //
        SetupHelpLabel.AutoSize = true;
        ConnectionLayout.SetColumnSpan(SetupHelpLabel, 2);
        SetupHelpLabel.Dock = DockStyle.Fill;
        SetupHelpLabel.Margin = new Padding(3, 10, 3, 6);
        SetupHelpLabel.MaximumSize = new Size(720, 0);
        SetupHelpLabel.Name = "SetupHelpLabel";
        SetupHelpLabel.TabIndex = 4;
        SetupHelpLabel.Text = "Use an Entra desktop app registration with a http://localhost redirect URI and delegated Azure Service Management permission (user_impersonation). Your organisation may require administrator consent.";
        //
        // SignInFlow
        //
        SignInFlow.AutoSize = true;
        ConnectionLayout.SetColumnSpan(SignInFlow, 2);
        SignInFlow.Controls.Add(_Connect);
        SignInFlow.Controls.Add(SetupLink);
        SignInFlow.Dock = DockStyle.Fill;
        SignInFlow.Name = "SignInFlow";
        SignInFlow.TabIndex = 5;
        //
        // _Connect
        //
        _Connect.AutoSize = true;
        _Connect.Name = "ConnectButton";
        _Connect.TabIndex = 0;
        _Connect.Text = "Sign in";
        _Connect.UseVisualStyleBackColor = true;
        _Connect.Click += Connect_Click;
        //
        // SetupLink
        //
        SetupLink.AutoSize = true;
        SetupLink.Margin = new Padding(12, 7, 3, 3);
        SetupLink.Name = "SetupLink";
        SetupLink.TabIndex = 1;
        SetupLink.TabStop = true;
        SetupLink.Text = "Application setup";
        SetupLink.LinkClicked += Setup_LinkClicked;
        //
        // SubscriptionsLabel
        //
        SubscriptionsLabel.AutoSize = true;
        ConnectionLayout.SetColumnSpan(SubscriptionsLabel, 2);
        SubscriptionsLabel.Margin = new Padding(3, 10, 3, 6);
        SubscriptionsLabel.Name = "SubscriptionsLabel";
        SubscriptionsLabel.TabIndex = 6;
        SubscriptionsLabel.Text = "Subscriptions (select one or more)";
        //
        // _Subscriptions
        //
        _Subscriptions.CheckOnClick = true;
        ConnectionLayout.SetColumnSpan(_Subscriptions, 2);
        _Subscriptions.Dock = DockStyle.Fill;
        _Subscriptions.HorizontalScrollbar = true;
        _Subscriptions.IntegralHeight = false;
        _Subscriptions.MinimumSize = new Size(0, 120);
        _Subscriptions.Name = "SubscriptionsList";
        _Subscriptions.TabIndex = 7;
        _Subscriptions.ItemCheck += Subscriptions_ItemCheck;
        //
        // SelectionFlow
        //
        SelectionFlow.AutoSize = true;
        ConnectionLayout.SetColumnSpan(SelectionFlow, 2);
        SelectionFlow.Controls.Add(_All);
        SelectionFlow.Controls.Add(_None);
        SelectionFlow.Dock = DockStyle.Fill;
        SelectionFlow.Name = "SelectionFlow";
        SelectionFlow.TabIndex = 8;
        //
        // _All
        //
        _All.AutoSize = true;
        _All.Enabled = false;
        _All.Name = "SelectAllButton";
        _All.TabIndex = 0;
        _All.Text = "Select all";
        _All.UseVisualStyleBackColor = true;
        _All.Click += All_Click;
        //
        // _None
        //
        _None.AutoSize = true;
        _None.Enabled = false;
        _None.Name = "SelectNoneButton";
        _None.TabIndex = 1;
        _None.Text = "Select none";
        _None.UseVisualStyleBackColor = true;
        _None.Click += None_Click;
        //
        // AccessLabel
        //
        AccessLabel.AutoSize = true;
        ConnectionLayout.SetColumnSpan(AccessLabel, 2);
        AccessLabel.Dock = DockStyle.Fill;
        AccessLabel.Margin = new Padding(3, 6, 3, 10);
        AccessLabel.MaximumSize = new Size(720, 0);
        AccessLabel.Name = "AccessLabel";
        AccessLabel.TabIndex = 9;
        AccessLabel.Text = "Only resources you can read are returned. Reader access across each selected subscription is needed for a complete inventory.";
        //
        // _Status
        //
        _Status.AutoSize = true;
        ConnectionLayout.SetColumnSpan(_Status, 2);
        _Status.Dock = DockStyle.Fill;
        _Status.MaximumSize = new Size(720, 0);
        _Status.Name = "StatusLabel";
        _Status.TabIndex = 10;
        _Status.Text = "Sign in to list enabled subscriptions in this tenant.";
        //
        // ActionsFlow
        //
        ActionsFlow.AutoSize = true;
        ConnectionLayout.SetColumnSpan(ActionsFlow, 2);
        ActionsFlow.Controls.Add(_Cancel);
        ActionsFlow.Controls.Add(_Import);
        ActionsFlow.Dock = DockStyle.Fill;
        ActionsFlow.FlowDirection = FlowDirection.RightToLeft;
        ActionsFlow.Margin = new Padding(0, 12, 0, 0);
        ActionsFlow.Name = "ActionsFlow";
        ActionsFlow.TabIndex = 11;
        //
        // _Cancel
        //
        _Cancel.AutoSize = true;
        _Cancel.DialogResult = DialogResult.Cancel;
        _Cancel.Name = "CancelButton";
        _Cancel.TabIndex = 1;
        _Cancel.Text = "Cancel";
        _Cancel.UseVisualStyleBackColor = true;
        //
        // _Import
        //
        _Import.AutoSize = true;
        _Import.Enabled = false;
        _Import.Name = "RetrieveButton";
        _Import.TabIndex = 0;
        _Import.Text = "Retrieve resources";
        _Import.UseVisualStyleBackColor = true;
        _Import.Click += Import_Click;
        //
        // _Progress
        //
        _Progress.Dock = DockStyle.Bottom;
        _Progress.Name = "QueryProgress";
        _Progress.Size = new Size(760, 4);
        _Progress.Style = ProgressBarStyle.Marquee;
        _Progress.TabIndex = 1;
        _Progress.Visible = false;
        //
        // AzureResourceGraphConnectionForm
        //
        AcceptButton = _Connect;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        CancelButton = _Cancel;
        ClientSize = new Size(760, 540);
        Controls.Add(ConnectionLayout);
        Controls.Add(_Progress);
        Font = new Font("Segoe UI", 9F);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(640, 520);
        Name = "AzureResourceGraphConnectionForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Azure Resource Graph";
        ConnectionLayout.ResumeLayout(false);
        ConnectionLayout.PerformLayout();
        SignInFlow.ResumeLayout(false);
        SignInFlow.PerformLayout();
        SelectionFlow.ResumeLayout(false);
        SelectionFlow.PerformLayout();
        ActionsFlow.ResumeLayout(false);
        ActionsFlow.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel ConnectionLayout;
    private Label TenantLabel;
    private TextBox _Tenant;
    private Label ClientIdLabel;
    private TextBox _ClientId;
    private Label SetupHelpLabel;
    private FlowLayoutPanel SignInFlow;
    private Button _Connect;
    private LinkLabel SetupLink;
    private Label SubscriptionsLabel;
    private CheckedListBox _Subscriptions;
    private FlowLayoutPanel SelectionFlow;
    private Button _All;
    private Button _None;
    private Label AccessLabel;
    private Label _Status;
    private FlowLayoutPanel ActionsFlow;
    private Button _Cancel;
    private Button _Import;
    private ProgressBar _Progress;
}
