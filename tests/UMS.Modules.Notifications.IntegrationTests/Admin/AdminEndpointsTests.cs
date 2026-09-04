using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Admin;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.IntegrationTests.Infrastructure;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.IntegrationTests.Admin;

/// <summary>NTF-14: admin/ops delivery-status and dead-letter triage, permission-gated (requirement-spec.md §6).</summary>
[Collection(NotificationsApiTestCollectionDefinition.Name)]
public class AdminEndpointsTests(NotificationsApiFixture fixture)
{
    [Fact]
    public async Task GetById_without_the_request_read_permission_is_forbidden()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var accessToken = await TestAuth.LoginAsync(client, user.Username, TestUsers.DefaultPassword);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notifications/requests/{Guid.NewGuid()}").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_returns_the_request_with_its_delivery_attempts()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword) = await TestDataSeeder.ProvisionNotificationsAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var submitted = await intake.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", $"invoice-{Guid.NewGuid():N}", Guid.NewGuid(), "{}"));
        Assert.True(submitted.IsSuccess);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notifications/requests/{submitted.Value}").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<NotificationRequestDto>();
        Assert.Equal(submitted.Value, dto!.Id);
        Assert.Equal(3, dto.Attempts.Count);
    }

    [Fact]
    public async Task GetById_for_an_unknown_id_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword) = await TestDataSeeder.ProvisionNotificationsAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notifications/requests/{Guid.NewGuid()}").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeadLetters_lists_a_terminally_failed_attempt()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword) = await TestDataSeeder.ProvisionNotificationsAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        // A recipient with no verified contact info at all -> every external channel dead-letters
        // immediately with NoContactInfo (§8 edge case), the cheapest way to reliably produce a
        // real dead-letter row for this assertion.
        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var dispatch = scope.ServiceProvider.GetRequiredService<UMS.Modules.Notifications.Application.Dispatch.NotificationDispatchService>();

        var recipientId = Guid.NewGuid(); // Never provisioned in Identity - GetContactInfoAsync returns null.
        var submitted = await intake.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", $"invoice-{Guid.NewGuid():N}", recipientId, "{}"));
        var stored = await scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>().GetByIdAsync(new NotificationRequestId(submitted.Value));
        var emailAttempt = stored!.Attempts.Single(a => a.Channel == NotificationChannel.Email);

        await dispatch.ProcessClaimedAsync(new ClaimedAttempt(emailAttempt.Id.Value, stored.Id.Value, 0));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/dead-letters?take=500").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var deadLetters = await response.Content.ReadFromJsonAsync<List<DeadLetterDto>>();
        Assert.Contains(deadLetters!, d => d.AttemptId == emailAttempt.Id.Value);
    }
}
