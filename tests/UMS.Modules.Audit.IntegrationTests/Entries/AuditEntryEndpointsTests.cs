using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.IntegrationTests.Infrastructure;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.IntegrationTests.Entries;

/// <summary>AUD-6/AUD-7: filtered listing and single-entry detail, permission-gated (requirement-spec.md audit §6, §4).</summary>
[Collection(AuditApiTestCollectionDefinition.Name)]
public class AuditEntryEndpointsTests(AuditApiFixture fixture)
{
    [Fact]
    public async Task Listing_entries_without_a_token_is_unauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/audit/entries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Listing_entries_without_the_audit_entry_read_permission_is_forbidden()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var accessToken = await TestAuth.LoginAsync(client, user.Username, TestUsers.DefaultPassword);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/audit/entries").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listing_entries_filters_by_entity_type_and_action()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var correlationId = $"list-filter-{Guid.NewGuid():N}";
        await WriteEntryAsync("Payment", "pay_1", AuditActions.Create, correlationId);
        await WriteEntryAsync("Payment", "pay_2", "refund", correlationId);
        await WriteEntryAsync("Grade", "grade_1", AuditActions.Update, correlationId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/audit/entries?entityType=Payment&action=refund").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<AuditLogEntryListPage>();
        var matched = page!.Items.Where(e => e.CorrelationId == correlationId).ToList();

        var entry = Assert.Single(matched);
        Assert.Equal("Payment", entry.EntityType);
        Assert.Equal("pay_2", entry.EntityId);
        Assert.Equal("refund", entry.Action);
    }

    [Fact]
    public async Task GetById_returns_the_full_entry_including_before_after_payload()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var correlationId = $"detail-{Guid.NewGuid():N}";
        var written = await WriteEntryAsync("Grade", "grade_detail", AuditActions.Update, correlationId, before: """{"score":60}""", after: """{"score":80}""");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit/entries/{written}").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<AuditLogEntryDto>();

        // Compared structurally, not by exact text - the jsonb column re-serializes on the way
        // back out of Postgres (e.g. "{"score":60}" round-trips as "{"score": 60}"), which is a
        // storage-layer formatting detail, not a semantic difference.
        Assert.Equal(60, System.Text.Json.JsonDocument.Parse(dto!.BeforeValue!).RootElement.GetProperty("score").GetInt32());
        Assert.Equal(80, System.Text.Json.JsonDocument.Parse(dto.AfterValue!).RootElement.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task GetById_for_an_unknown_id_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/audit/entries/01UNKNOWNXXXXXXXXXXXXXXXXX").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<string> WriteEntryAsync(string entityType, string entityId, string action, string correlationId, string? before = null, string? after = "{}")
    {
        using var scope = fixture.Services.CreateScope();
        var identityUnitOfWork = scope.ServiceProvider.GetRequiredService<UMS.Modules.Identity.Application.Abstractions.IUnitOfWork>();
        var auditRecorder = scope.ServiceProvider.GetRequiredService<UMS.Shared.Audit.IAuditRecorder>();
        var entries = scope.ServiceProvider.GetRequiredService<UMS.Modules.Audit.Application.Abstractions.IAuditLogEntryRepository>();

        var request = new RecordAuditEntryRequest(
            ActorId: "system:test-writer",
            ActorType: AuditActorType.System,
            IpAddress: null,
            Application: "system",
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            BeforeValueJson: before,
            AfterValueJson: after,
            CorrelationId: correlationId);

        await using var transaction = await identityUnitOfWork.BeginTransactionAsync();
        var result = await auditRecorder.RecordEntryAsync(request, transaction.DbTransaction);
        Assert.True(result.IsSuccess);
        await transaction.CommitAsync();

        var history = await entries.GetEntityHistoryAsync(entityType, entityId);
        return history.Single(e => e.CorrelationId == correlationId).Id.Value;
    }
}
