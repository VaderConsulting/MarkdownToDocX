using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace LiveEditor.Azure;

/// <summary>Reads Azure public-cloud inventory using the signed-in identity's ARM permissions.</summary>
internal sealed class AzureResourceGraphClient
{
    internal const string InventoryQuery = "Resources | project id, subscriptionId, resourceGroup, name, type, location, kind, sku, tags | order by id asc";
    private const string GraphUrl = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01";
    private static readonly TokenRequestContext TokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
    private readonly HttpClient _Http;
    private readonly TokenCredential _Credential;
    private readonly string _TenantId;

    public AzureResourceGraphClient(HttpClient Http, TokenCredential Credential, string TenantId)
    {
        _Http = Http;
        _Credential = Credential;
        _TenantId = Guid.Parse(TenantId).ToString();
    }

    public async Task<List<AzureSubscription>> GetSubscriptionsAsync(CancellationToken Cancellation)
    {
        List<AzureSubscription> Result = [];
        string? Next = "https://management.azure.com/subscriptions?api-version=2022-12-01";
        HashSet<string> Seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(Next))
        {
            if (!Seen.Add(Next))
            {
                throw new InvalidOperationException("Azure returned a repeated subscription page.");
            }

            using JsonDocument Page = await SendAsync(HttpMethod.Get, Next, null, Cancellation).ConfigureAwait(false);
            foreach (JsonElement Item in Page.RootElement.GetProperty("value").EnumerateArray())
            {
                string Tenant = ReadString(Item, "tenantId");
                string Id = ReadString(Item, "subscriptionId");
                if (string.Equals(Tenant, _TenantId, StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParse(Id, out _)
                    && string.Equals(ReadString(Item, "state"), "Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    Result.Add(new AzureSubscription(Id, ReadString(Item, "displayName")));
                }
            }

            Next = ReadString(Page.RootElement, "nextLink");
        }

        return [.. Result.DistinctBy(Item => Item.Id, StringComparer.OrdinalIgnoreCase).OrderBy(Item => Item.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<List<AzureResourceGraphRecord>> GetResourcesAsync(
        IReadOnlyList<string> SubscriptionIds, IProgress<int>? Progress, CancellationToken Cancellation)
    {
        if (SubscriptionIds.Count == 0 || SubscriptionIds.Any(Id => !Guid.TryParse(Id, out _)))
        {
            throw new ArgumentException("Select at least one valid Azure subscription.", nameof(SubscriptionIds));
        }

        Dictionary<string, AzureResourceGraphRecord> Resources = new Dictionary<string, AzureResourceGraphRecord>(StringComparer.OrdinalIgnoreCase);
        // Explicit scopes avoid including subscriptions delegated from another tenant.
        foreach (string[] Batch in SubscriptionIds.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(1000))
        {
            string? SkipToken = null;
            HashSet<string> Tokens = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                Dictionary<string, object> Options = new Dictionary<string, object>
                {
                    ["$top"] = 1000,
                    ["resultFormat"] = "objectArray"
                };
                if (SkipToken != null)
                {
                    Options["$skipToken"] = SkipToken;
                }

                string Body = JsonSerializer.Serialize(new { subscriptions = Batch, query = InventoryQuery, options = Options });
                using JsonDocument Page = await SendAsync(HttpMethod.Post, GraphUrl, Body, Cancellation).ConfigureAwait(false);
                JsonElement Data = Page.RootElement.GetProperty("data");
                if (Data.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("Azure returned an unexpected Resource Graph result format.");
                }

                foreach (JsonElement Item in Data.EnumerateArray())
                {
                    AzureResourceGraphRecord Record = AzureResourceGraphReader.ReadJsonObject(Item);
                    if (string.IsNullOrWhiteSpace(Record.Id) || !Batch.Contains(Record.SubscriptionId, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Azure returned a resource outside the selected subscriptions or without an ID.");
                    }

                    Resources[Record.Id] = Record;
                }

                Progress?.Report(Resources.Count);
                SkipToken = ReadString(Page.RootElement, "$skipToken");
                bool Truncated = string.Equals(ReadString(Page.RootElement, "resultTruncated"), "true", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(SkipToken))
                {
                    SkipToken = null;
                    if (Truncated)
                    {
                        throw new InvalidOperationException("Azure truncated the inventory without a continuation token. No partial inventory was imported.");
                    }
                }
                else if (!Tokens.Add(SkipToken))
                {
                    throw new InvalidOperationException("Azure returned a repeated Resource Graph continuation token. No partial inventory was imported.");
                }
            }
            while (SkipToken != null);
        }

        Cancellation.ThrowIfCancellationRequested();
        return [.. Resources.Values.OrderBy(Item => Item.Id, StringComparer.OrdinalIgnoreCase)];
    }

    private async Task<JsonDocument> SendAsync(HttpMethod Method, string Url, string? Body, CancellationToken Cancellation)
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? Endpoint)
            || Endpoint.Scheme != Uri.UriSchemeHttps || Endpoint.Host != "management.azure.com"
            || !Endpoint.IsDefaultPort || Endpoint.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("Azure returned an unexpected API endpoint.");
        }

        for (int Attempt = 0; ; Attempt++)
        {
            AccessToken Token = await _Credential.GetTokenAsync(TokenContext, Cancellation).ConfigureAwait(false);
            using HttpRequestMessage Request = new HttpRequestMessage(Method, Endpoint);
            Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token.Token);
            if (Body != null)
            {
                Request.Content = new StringContent(Body, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage Response = await _Http.SendAsync(Request, Cancellation).ConfigureAwait(false);
            if ((Response.StatusCode == HttpStatusCode.TooManyRequests || Response.StatusCode == HttpStatusCode.ServiceUnavailable) && Attempt < 3)
            {
                TimeSpan Delay = Response.Headers.RetryAfter?.Delta
                    ?? (Response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.FromSeconds(1 << Attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(Delay.TotalSeconds, 1, 30)), Cancellation).ConfigureAwait(false);
                continue;
            }

            string Text = await Response.Content.ReadAsStringAsync(Cancellation).ConfigureAwait(false);
            if (!Response.IsSuccessStatusCode)
            {
                string Detail = string.Empty;
                try
                {
                    using JsonDocument Error = JsonDocument.Parse(Text);
                    Detail = ReadString(Error.RootElement.GetProperty("error"), "message");
                    if (Detail.Length > 600)
                    {
                        Detail = Detail.Substring(0, 600);
                    }
                }
                catch (JsonException) { }
                catch (KeyNotFoundException) { }

                throw new HttpRequestException($"Azure request failed ({(int)Response.StatusCode} {Response.ReasonPhrase}). {Detail}", null, Response.StatusCode);
            }

            return JsonDocument.Parse(Text);
        }
    }

    private static string ReadString(JsonElement Item, string Name)
    {
        return Item.TryGetProperty(Name, out JsonElement Value) && Value.ValueKind != JsonValueKind.Null ? Value.ToString() : string.Empty;
    }
}

internal sealed record AzureSubscription(string Id, string Name)
{
    public override string ToString() => $"{Name} ({Id})";
}
