using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Notifications.Application.Templates;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.IntegrationTests.Infrastructure;

namespace UMS.Modules.Notifications.IntegrationTests.Templates;

/// <summary>NTF-8: admin template list/manage endpoints, permission-gated (requirement-spec.md §6).</summary>
[Collection(NotificationsApiTestCollectionDefinition.Name)]
public class TemplateEndpointsTests(NotificationsApiFixture fixture)
{
    [Fact]
    public async Task Listing_templates_without_a_token_is_unauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/notifications/templates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Listing_templates_without_the_template_manage_permission_is_forbidden()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var accessToken = await TestAuth.LoginAsync(client, user.Username, TestUsers.DefaultPassword);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/templates").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Updating_a_template_upserts_a_translation_and_is_visible_in_the_list()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword) = await TestDataSeeder.ProvisionNotificationsAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var eventType = $"IntegrationTestEvent-{Guid.NewGuid():N}";
        var templateId = await CreateTemplateAsync(client, accessToken, eventType, NotificationChannel.Email);

        var updateBody = new { languageCode = "en", subject = "Welcome", body = "Hello {{name}}", pushTitle = (string?)null, deepLink = (string?)null };
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/notifications/templates/{templateId}") { Content = JsonContent.Create(updateBody) }.WithBearerToken(accessToken);
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.Contains(updated!.Translations, t => t.LanguageCode == "en" && t.Body == "Hello {{name}}");

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/templates?take=200").WithBearerToken(accessToken);
        var listResponse = await client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var page = await listResponse.Content.ReadFromJsonAsync<List<TemplateDto>>();
        Assert.Contains(page!, t => t.Id == templateId);
    }

    [Fact]
    public async Task Updating_an_Email_template_without_a_subject_is_rejected()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword) = await TestDataSeeder.ProvisionNotificationsAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var eventType = $"IntegrationTestEvent-{Guid.NewGuid():N}";
        var templateId = await CreateTemplateAsync(client, accessToken, eventType, NotificationChannel.Email);

        var updateBody = new { languageCode = "en", subject = (string?)null, body = "Hello", pushTitle = (string?)null, deepLink = (string?)null };
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/notifications/templates/{templateId}") { Content = JsonContent.Create(updateBody) }.WithBearerToken(accessToken);
        var updateResponse = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Creating_a_template_without_the_template_manage_permission_is_forbidden()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var accessToken = await TestAuth.LoginAsync(client, user.Username, TestUsers.DefaultPassword);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notifications/templates")
        {
            Content = JsonContent.Create(new { eventType = "IntegrationTestEvent", channel = NotificationChannel.Email }),
        }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_template_twice_for_the_same_event_type_and_channel_returns_the_same_row()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword) = await TestDataSeeder.ProvisionNotificationsAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);
        var eventType = $"IntegrationTestEvent-{Guid.NewGuid():N}";

        var firstId = await CreateTemplateAsync(client, accessToken, eventType, NotificationChannel.Sms);
        var secondId = await CreateTemplateAsync(client, accessToken, eventType, NotificationChannel.Sms);

        Assert.Equal(firstId, secondId);
    }

    private static async Task<Guid> CreateTemplateAsync(HttpClient client, string accessToken, string eventType, NotificationChannel channel)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notifications/templates")
        {
            Content = JsonContent.Create(new { eventType, channel }),
        }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TemplateDto>();
        return dto!.Id;
    }
}
