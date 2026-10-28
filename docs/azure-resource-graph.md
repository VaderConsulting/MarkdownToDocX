# Azure Resource Graph import

LiveEditor provides two commands under **File > Import > Azure**:

- **Resource Graph** signs in to Azure and retrieves inventory from selected subscriptions.
- **Resource Graph Export (*.csv)** imports an existing Azure Resource Graph CSV export.

Both routes use the same category, grouping and relationship options to create an enterprise diagram. The defaults omit infrastructure detail such as disks and network interfaces; choose the relevant categories to include them.

## Set up direct access

1. In the target tenant, register an application in Microsoft Entra ID. A single-tenant registration is sufficient when querying only that tenant.
2. Under **Authentication**, add a **Mobile and desktop applications** platform and the redirect URI `http://localhost`.
3. Under **API permissions**, add **Azure Service Management > Delegated permissions > user_impersonation**. Obtain administrator consent if your organisation's policy requires it.
4. Copy the **Directory (tenant) ID** and **Application (client) ID** from the registration overview into LiveEditor's Resource Graph dialog.
5. Select **Sign in**, complete browser authentication (including MFA when required), check the desired subscriptions, then select **Retrieve resources**.
6. Choose resource categories and diagram options in the existing import dialog.

The desktop app uses a public client registration and requires no client secret. LiveEditor remembers only the tenant and client IDs in its normal session settings. Tokens are held in memory by the authentication library; LiveEditor does not persist them.

The signed-in user needs Azure RBAC read permissions. For a complete subscription inventory, grant Reader at each selected subscription or an inherited management group scope. Tenant membership or directory administrator status alone does not grant this access. Resource Graph can silently omit resources the user cannot read, even when a query succeeds.

## Scope and completeness

This connection targets Azure public cloud. The subscription chooser includes accessible enabled subscriptions whose tenant ID matches the entered tenant, excluding subscriptions delegated from other tenants. Select all or a subset explicitly.

The query retrieves Resource Graph's `Resources` table, projecting `id`, `subscriptionId`, `resourceGroup`, `name`, `type`, `location`, `kind`, `sku` and `tags`. Resource types held in other Resource Graph tables and data-plane objects are outside this inventory. Resource Graph's index can lag recent changes.

Results are fetched in pages of up to 1,000 rows, with continuation tokens and resource-ID deduplication. Throttled and temporarily unavailable requests are retried up to three times. Cancelling, failed requests, repeated continuation tokens, or truncation without a continuation token leave the current document intact. Azure inventories are not transactional snapshots; changes during a long query can affect the returned set.

## References

- [Configure a desktop application](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-registration)
- [Resource Graph REST API](https://learn.microsoft.com/en-us/rest/api/azureresourcegraph/resourcegraph/resources/resources?view=rest-azureresourcegraph-resourcegraph-2024-04-01)
- [Permissions and resource coverage](https://learn.microsoft.com/en-us/azure/governance/resource-graph/overview)
- [Pagination guidance](https://learn.microsoft.com/en-us/azure/governance/resource-graph/concepts/paging-results)
