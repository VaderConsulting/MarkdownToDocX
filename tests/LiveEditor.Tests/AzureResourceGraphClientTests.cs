using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using LiveEditor.Azure;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class AzureResourceGraphClientTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";
    private const string Subscription = "22222222-2222-2222-2222-222222222222";
    private const string OtherSubscription = "33333333-3333-3333-3333-333333333333";

    [Test]
    public async Task Subscriptions_FollowsPagesAndExcludesOtherTenantsAndDisabledSubscriptions()
    {
        using StubHandler Handler = new StubHandler();
        Handler.Replies.Enqueue(_ => Json(new
        {
            value = new[]
            {
                new { subscriptionId = Subscription, displayName = "Selected tenant", tenantId = Tenant, state = "Enabled" },
                new { subscriptionId = OtherSubscription, displayName = "Other tenant", tenantId = OtherSubscription, state = "Enabled" }
            },
            nextLink = "https://management.azure.com/subscriptions?api-version=2022-12-01&next=2"
        }));
        Handler.Replies.Enqueue(_ => Json(new
        {
            value = new[] { new { subscriptionId = OtherSubscription, displayName = "Disabled", tenantId = Tenant, state = "Disabled" } }
        }));
        using HttpClient Http = new HttpClient(Handler);
        List<AzureSubscription> Result = await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetSubscriptionsAsync(default);
        Assert.That(Result.Select(Item => Item.Id), Is.EqualTo([Subscription]));
        Assert.That(Handler.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Inventory_PreservesScopeAndQueryAcrossPagesAndMapsStructuredFields()
    {
        using StubHandler Handler = new StubHandler();
        string? FirstQuery = null;
        Handler.Replies.Enqueue(Body =>
        {
            using JsonDocument Request = JsonDocument.Parse(Body!);
            Assert.That(Request.RootElement.GetProperty("subscriptions").EnumerateArray().Select(Id => Id.GetString()), Is.EqualTo([Subscription]));
            FirstQuery = Request.RootElement.GetProperty("query").GetString();
            Assert.That(Request.RootElement.GetProperty("options").GetProperty("resultFormat").GetString(), Is.EqualTo("objectArray"));
            return Page([Resource("one")], "next");
        });
        Handler.Replies.Enqueue(Body =>
        {
            using JsonDocument Request = JsonDocument.Parse(Body!);
            Assert.That(Request.RootElement.GetProperty("subscriptions")[0].GetString(), Is.EqualTo(Subscription));
            Assert.That(Request.RootElement.GetProperty("query").GetString(), Is.EqualTo(FirstQuery));
            Assert.That(Request.RootElement.GetProperty("options").GetProperty("$skipToken").GetString(), Is.EqualTo("next"));
            return Page([Resource("two"), Resource("one")]);
        });
        using HttpClient Http = new HttpClient(Handler);
        List<AzureResourceGraphRecord> Result = await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, default);
        Assert.That(Result.Select(Item => Item.Name), Is.EqualTo(["one", "two"]));
        Assert.That(Result[0].TagsJson, Does.Contain("production"));
        Assert.That(Result[0].Sku, Does.Contain("Standard"));
    }

    [Test]
    public async Task Inventory_TruncatedWithoutTokenDoesNotReturnPartialImport()
    {
        using StubHandler Handler = new StubHandler();
        Handler.Replies.Enqueue(_ => Page([Resource("one")], null, true));
        using HttpClient Http = new HttpClient(Handler);
        InvalidOperationException? Error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, default));
        Assert.That(Error!.Message, Does.Contain("truncated"));
    }

    [Test]
    public async Task Inventory_RepeatedTokenStopsRatherThanLooping()
    {
        using StubHandler Handler = new StubHandler();
        Handler.Replies.Enqueue(_ => Page([Resource("one")], "same"));
        Handler.Replies.Enqueue(_ => Page([Resource("two")], "same"));
        using HttpClient Http = new HttpClient(Handler);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, default));
        Assert.That(Handler.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Inventory_RejectsResourceOutsideSelection()
    {
        using StubHandler Handler = new StubHandler();
        Handler.Replies.Enqueue(_ => Page([Resource("unexpected", OtherSubscription)]));
        using HttpClient Http = new HttpClient(Handler);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, default));
    }

    [Test]
    public async Task Subscriptions_RejectsUntrustedContinuationEndpoint()
    {
        using StubHandler Handler = new StubHandler();
        Handler.Replies.Enqueue(_ => Json(new { value = Array.Empty<object>(), nextLink = "https://example.com/steal-token" }));
        using HttpClient Http = new HttpClient(Handler);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetSubscriptionsAsync(default));
        Assert.That(Handler.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Inventory_CancellationDoesNotSendARequest()
    {
        using StubHandler Handler = new StubHandler();
        using HttpClient Http = new HttpClient(Handler);
        using CancellationTokenSource Cancellation = new CancellationTokenSource();
        Cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, Cancellation.Token));
        Assert.That(Handler.Calls, Is.Zero);
    }

    [Test]
    public async Task Inventory_RetriesThrottlingWithoutChangingQuery()
    {
        using StubHandler Handler = new StubHandler();
        string? Original = null;
        Handler.Replies.Enqueue(Body =>
        {
            Original = Body;
            HttpResponseMessage Response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            Response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Response;
        });
        Handler.Replies.Enqueue(Body =>
        {
            Assert.That(Body, Is.EqualTo(Original));
            return Page([Resource("one")]);
        });
        using HttpClient Http = new HttpClient(Handler);
        Assert.That(await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, default), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Inventory_ForbiddenReportsAzureError()
    {
        using StubHandler Handler = new StubHandler();
        Handler.Replies.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Reader access required\"}}")
        });
        using HttpClient Http = new HttpClient(Handler);
        HttpRequestException? Error = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await new AzureResourceGraphClient(Http, new StubCredential(), Tenant).GetResourcesAsync([Subscription], null, default));
        Assert.That(Error!.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(Error.Message, Does.Contain("Reader access required"));
    }

    private static object Resource(string Name, string SubscriptionId = Subscription) => new
    {
        id = $"/subscriptions/{SubscriptionId}/resourceGroups/test/providers/Microsoft.Web/sites/{Name}",
        subscriptionId = SubscriptionId, resourceGroup = "test", name = Name, type = "microsoft.web/sites",
        location = "australiaeast", kind = "app", sku = new { name = "Standard" }, tags = new { environment = "production" }
    };

    private static HttpResponseMessage Page(object[] Data, string? Token = null, bool Truncated = false) => Json(new Dictionary<string, object?>
    {
        ["data"] = Data, ["$skipToken"] = Token, ["resultTruncated"] = Truncated ? "true" : "false"
    });

    private static HttpResponseMessage Json(object Value) => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(Value), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        public Queue<Func<string?, HttpResponseMessage>> Replies { get; } = new Queue<Func<string?, HttpResponseMessage>>();
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage Request, CancellationToken Cancellation)
        {
            Calls++;
            Assert.That(Request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(Request.RequestUri!.Host, Is.EqualTo("management.azure.com"));
            string? Body = Request.Content == null ? null : await Request.Content.ReadAsStringAsync(Cancellation);
            return Replies.Dequeue()(Body);
        }
    }

    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext Context, CancellationToken Cancellation)
        {
            Assert.That(Context.Scopes, Is.EqualTo(["https://management.azure.com/.default"]));
            return new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext Context, CancellationToken Cancellation) => ValueTask.FromResult(GetToken(Context, Cancellation));
    }
}
