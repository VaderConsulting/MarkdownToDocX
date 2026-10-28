using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Azure.Identity;
using LiveEditor.Azure;

namespace LiveEditor;

/// <summary>Signs in to a tenant, selects subscriptions and retrieves their Resource Graph inventory.</summary>
internal sealed partial class AzureResourceGraphConnectionForm : Form
{
    private readonly HttpClient _Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
    private CancellationTokenSource? _Operation;
    private AzureResourceGraphClient? _Graph;
    private bool _CloseAfterCancellation;

    public AzureResourceGraphConnectionForm() : this(null, null)
    {
    }

    public AzureResourceGraphConnectionForm(string? TenantId, string? ClientId)
    {
        InitializeComponent();
        _Tenant.Text = TenantId ?? string.Empty;
        _ClientId.Text = ClientId ?? string.Empty;
    }

    private void Setup_LinkClicked(object? Sender, LinkLabelLinkClickedEventArgs E)
    {
        Process.Start(new ProcessStartInfo("https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-registration") { UseShellExecute = true });
    }

    private void All_Click(object? Sender, EventArgs E) => SetSelection(true);
    private void None_Click(object? Sender, EventArgs E) => SetSelection(false);
    private void Identity_TextChanged(object? Sender, EventArgs E) => ClearConnection();
    private void Subscriptions_ItemCheck(object? Sender, ItemCheckEventArgs E)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(UpdateImportAvailability);
        }
    }

    public string TenantId => _Tenant.Text.Trim();
    public string ClientId => _ClientId.Text.Trim();
    public IReadOnlyList<AzureResourceGraphRecord> Records { get; private set; } = Array.Empty<AzureResourceGraphRecord>();

    private async void Connect_Click(object? Sender, EventArgs E)
    {
        if (!Guid.TryParse(TenantId, out _) || !Guid.TryParse(ClientId, out _))
        {
            MessageBox.Show(this, "Enter a valid tenant ID and application (client) ID.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ClearConnection();
        await RunAsync(async Cancellation =>
        {
            _Status.Text = "Complete sign-in in your browser…";
            InteractiveBrowserCredential Credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = TenantId,
                ClientId = ClientId,
                RedirectUri = new Uri("http://localhost")
            });
            AzureResourceGraphClient Graph = new AzureResourceGraphClient(_Http, Credential, TenantId);
            using CancellationTokenSource SignInTimeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
            SignInTimeout.CancelAfter(TimeSpan.FromMinutes(5));
            List<AzureSubscription> Subscriptions = await Graph.GetSubscriptionsAsync(SignInTimeout.Token);
            Cancellation.ThrowIfCancellationRequested();
            _Graph = Graph;
            foreach (AzureSubscription Subscription in Subscriptions)
            {
                _Subscriptions.Items.Add(Subscription, false);
            }

            _Status.Text = Subscriptions.Count == 0
                ? "No accessible enabled subscriptions were found in this tenant. Check the tenant and your Azure permissions."
                : $"Found {Subscriptions.Count} subscriptions. Select those to query.";
            AcceptButton = _Import;
        });
    }

    private async void Import_Click(object? Sender, EventArgs E)
    {
        AzureResourceGraphClient? Graph = _Graph;
        string[] Selected = [.. _Subscriptions.CheckedItems.Cast<AzureSubscription>().Select(Item => Item.Id)];
        if (Graph == null || Selected.Length == 0)
        {
            return;
        }

        await RunAsync(async Cancellation =>
        {
            _Status.Text = "Retrieving resources…";
            Progress<int> Progress = new Progress<int>(Count =>
            {
                if (!IsDisposed && !Disposing)
                {
                    _Status.Text = $"Retrieved {Count:N0} resources…";
                }
            });
            List<AzureResourceGraphRecord> Retrieved = await Graph.GetResourcesAsync(Selected, Progress, Cancellation);
            Cancellation.ThrowIfCancellationRequested();
            if (Retrieved.Count == 0)
            {
                _Status.Text = "No resources were returned. Check the selected subscriptions and your read permissions.";
                return;
            }

            Records = Retrieved;
        });
        if (Records.Count > 0 && !_CloseAfterCancellation && !IsDisposed)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> Action)
    {
        if (_Operation != null)
        {
            return;
        }

        using CancellationTokenSource Operation = new CancellationTokenSource();
        _Operation = Operation;
        SetBusy(true);
        try
        {
            await Action(Operation.Token);
        }
        catch (OperationCanceledException)
        {
            _Status.Text = Operation.IsCancellationRequested ? "Cancelled." : "The Azure request timed out. Try again.";
        }
        catch (Exception Error)
        {
            _Status.Text = "Azure connection failed. You can retry.";
            if (!_CloseAfterCancellation)
            {
                MessageBox.Show(this, Error.Message, "Azure Resource Graph", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _Operation = null;
            SetBusy(false);
            if (_CloseAfterCancellation)
            {
                Close();
            }
        }
    }

    private void SetBusy(bool Busy)
    {
        _Tenant.Enabled = _ClientId.Enabled = _Connect.Enabled = _Subscriptions.Enabled = !Busy;
        _All.Enabled = _None.Enabled = !Busy && _Subscriptions.Items.Count > 0;
        _Progress.Visible = Busy;
        UpdateImportAvailability();
    }

    private void UpdateImportAvailability()
    {
        _Import.Enabled = _Operation == null && _Graph != null && _Subscriptions.CheckedItems.Count > 0;
    }

    private void ClearConnection()
    {
        _Graph = null;
        _Subscriptions.Items.Clear();
        _All.Enabled = _None.Enabled = _Import.Enabled = false;
        _Status.Text = "Sign in to list enabled subscriptions in this tenant.";
        AcceptButton = _Connect;
    }

    private void SetSelection(bool Selected)
    {
        for (int Index = 0; Index < _Subscriptions.Items.Count; Index++)
        {
            _Subscriptions.SetItemChecked(Index, Selected);
        }

        UpdateImportAvailability();
    }

    protected override void OnFormClosing(FormClosingEventArgs E)
    {
        if (_Operation != null)
        {
            E.Cancel = true;
            _CloseAfterCancellation = true;
            _Operation.Cancel();
            _Status.Text = "Cancelling…";
            DialogResult = DialogResult.None;
        }

        base.OnFormClosing(E);
    }

}
