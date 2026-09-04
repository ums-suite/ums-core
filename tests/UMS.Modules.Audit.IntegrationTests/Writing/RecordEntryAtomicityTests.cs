using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.IntegrationTests.Infrastructure;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.IntegrationTests.Writing;

/// <summary>
/// AUD-1's core promise (ADR-0012): a business mutation and its audit entry commit together, or
/// neither commits at all. <see cref="UMS.Modules.Identity.Application.Users.UserStatusService"/>
/// (suspend/reactivate) is the one real, wired-up production call site this pass added (see that
/// class's own remarks) - used here both as an end-to-end proof through the real HTTP API and, for
/// the rollback half, driven directly against the DI container so the failure can be forced
/// deterministically.
/// </summary>
[Collection(AuditApiTestCollectionDefinition.Name)]
public class RecordEntryAtomicityTests(AuditApiFixture fixture)
{
    [Fact]
    public async Task Suspending_a_user_through_the_real_http_api_commits_a_matching_audit_entry()
    {
        using var client = fixture.CreateClient();
        var (adminUserId, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var target = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest($"target-{Guid.NewGuid():N}", $"target-{Guid.NewGuid():N}@example.edu.bd", "Target", "User", null, null, null, null, "a-strong-p@ssw0rd"));
        target.EnsureSuccessStatusCode();
        var targetUser = await target.Content.ReadFromJsonAsync<UserDto>();

        var statusRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/identity/users/{targetUser!.Id}/status")
        {
            Content = JsonContent.Create(new { status = "Suspended" }),
        }.WithBearerToken(accessToken);
        var statusResponse = await client.SendAsync(statusRequest);
        statusResponse.EnsureSuccessStatusCode();

        var historyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit/entities/User/{targetUser.Id}/history").WithBearerToken(accessToken);
        var historyResponse = await client.SendAsync(historyRequest);
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>();

        var suspendEntry = Assert.Single(history!);
        Assert.Equal("suspend", suspendEntry.Action);
        Assert.Equal("User", suspendEntry.EntityType);
        Assert.Equal(targetUser.Id.ToString(), suspendEntry.EntityId);
        Assert.Equal(adminUserId.ToString(), suspendEntry.ActorId);
        // Compared structurally, not by exact text - jsonb re-serializes on the way back out of
        // Postgres (a storage-layer formatting detail, not a semantic difference).
        Assert.Equal("Active", System.Text.Json.JsonDocument.Parse(suspendEntry.BeforeValue!).RootElement.GetProperty("status").GetString());
        Assert.Equal("Suspended", System.Text.Json.JsonDocument.Parse(suspendEntry.AfterValue!).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_failed_RecordEntry_call_rolls_back_the_hosts_own_mutation_too()
    {
        using var client = fixture.CreateClient();
        await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var user = await TestUsers.ProvisionAsync(client);

        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var auditRecorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

        var domainUser = await users.GetByIdAsync(new UserId(user.Id));
        Assert.NotNull(domainUser);

        await using var transaction = await unitOfWork.BeginTransactionAsync();

        domainUser!.Suspend(DateTimeOffset.UtcNow);

        // Deliberately invalid RecordEntry request (empty entity type) - simulates the "in-transaction
        // Audit write itself fails" edge case (edge-cases.md) without needing a real infrastructure
        // fault. The host is responsible for rolling back its own mutation when this happens.
        var invalidRequest = new RecordAuditEntryRequest(
            ActorId: user.Id.ToString(),
            ActorType: AuditActorType.User,
            IpAddress: null,
            Application: "ums-admin-web",
            EntityType: string.Empty,
            EntityId: user.Id.ToString(),
            Action: AuditActions.Update,
            BeforeValueJson: null,
            AfterValueJson: null,
            CorrelationId: "corr-atomicity-test");

        var auditResult = await auditRecorder.RecordEntryAsync(invalidRequest, transaction.DbTransaction);
        Assert.True(auditResult.IsFailure);

        await transaction.RollbackAsync();

        using var verifyScope = fixture.Services.CreateScope();
        var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var reloaded = await verifyUsers.GetByIdAsync(new UserId(user.Id));

        Assert.Equal(UserStatus.Active, reloaded!.Status);
    }
}
