using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Dispatch;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Application.Templates;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.IntegrationTests.Infrastructure;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.IntegrationTests.InApp;

/// <summary>NTF-12: the in-app notification center's own read API.</summary>
[Collection(NotificationsApiTestCollectionDefinition.Name)]
public class NotificationCenterEndpointsTests(NotificationsApiFixture fixture)
{
    [Fact]
    public async Task Listing_my_notifications_without_a_token_is_unauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/notifications/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_delivered_InApp_notification_appears_in_the_recipients_own_list_and_unread_count_and_can_be_marked_read()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var accessToken = await TestAuth.LoginAsync(client, user.Username, TestUsers.DefaultPassword);

        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var requests = scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>();
        var dispatch = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();
        var templates = scope.ServiceProvider.GetRequiredService<TemplateManagementService>();

        await templates.UpdateAsync(
            (await templates.GetOrCreateAsync("GradePublished", NotificationChannel.InApp)).Value.Id,
            new UpsertTemplateTranslationCommand("en", null, "Your grade has been published.", null, null));

        var submitted = await intake.SubmitAsync(new SubmitNotificationRequestCommand("academic", "GradePublished", $"grade-{Guid.NewGuid():N}", user.Id, "{}"));
        var request = await requests.GetByIdAsync(new NotificationRequestId(submitted.Value));
        var inAppAttempt = request!.Attempts.Single(a => a.Channel == NotificationChannel.InApp);
        await dispatch.ProcessClaimedAsync(new ClaimedAttempt(inAppAttempt.Id.Value, request.Id.Value, 0));

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/me").WithBearerToken(accessToken);
        var listResponse = await client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var notifications = await listResponse.Content.ReadFromJsonAsync<List<NotificationDeliveryAttemptDto>>();
        Assert.Contains(notifications!, n => n.Id == inAppAttempt.Id.Value && n.RenderedBody == "Your grade has been published.");

        var unreadRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/me/unread-count").WithBearerToken(accessToken);
        var unreadResponse = await client.SendAsync(unreadRequest);
        unreadResponse.EnsureSuccessStatusCode();
        var unread = await unreadResponse.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.True(unread!.UnreadCount >= 1);

        var markReadRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/notifications/me/{inAppAttempt.Id.Value}/read").WithBearerToken(accessToken);
        var markReadResponse = await client.SendAsync(markReadRequest);
        Assert.Equal(HttpStatusCode.NoContent, markReadResponse.StatusCode);
    }

    [Fact]
    public async Task Marking_another_recipients_notification_read_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var owner = await TestUsers.ProvisionAsync(client);
        var intruder = await TestUsers.ProvisionAsync(client);
        var intruderToken = await TestAuth.LoginAsync(client, intruder.Username, TestUsers.DefaultPassword);

        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var requests = scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>();
        var dispatch = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();
        var templates = scope.ServiceProvider.GetRequiredService<TemplateManagementService>();
        await templates.UpdateAsync(
            (await templates.GetOrCreateAsync("LeaveApproval", NotificationChannel.InApp)).Value.Id,
            new UpsertTemplateTranslationCommand("en", null, "Your leave was approved.", null, null));

        var submitted = await intake.SubmitAsync(new SubmitNotificationRequestCommand("faculty", "LeaveApproval", $"leave-{Guid.NewGuid():N}", owner.Id, "{}"));
        var request = await requests.GetByIdAsync(new NotificationRequestId(submitted.Value));
        var inAppAttempt = request!.Attempts.Single(a => a.Channel == NotificationChannel.InApp);
        await dispatch.ProcessClaimedAsync(new ClaimedAttempt(inAppAttempt.Id.Value, request.Id.Value, 0));

        var markReadRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/notifications/me/{inAppAttempt.Id.Value}/read").WithBearerToken(intruderToken);
        var markReadResponse = await client.SendAsync(markReadRequest);

        Assert.Equal(HttpStatusCode.NotFound, markReadResponse.StatusCode);
    }

    private sealed record UnreadCountResponse(int UnreadCount);
}
