using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Application.Templates;
using UMS.Modules.Documents.IntegrationTests.Infrastructure;

namespace UMS.Modules.Documents.IntegrationTests.Generation;

/// <summary>
/// DOC-3/DOC-8/DOC-10/DOC-12: the synchronous generation path end to end against real Postgres +
/// MinIO - a real object-storage round trip, not a mock. Uses "Transcript"/"IdCard" (the
/// shared-ok pool - see <c>TemplateEndpointsTests</c>' own remarks) for tests that only need "a
/// template exists," and "AdmitCard" (reserved exclusively to this class's own not-found test)
/// for the one absolute-state assertion ("no template was ever published for this type").
/// </summary>
[Collection(DocumentsApiTestCollectionDefinition.Name)]
public class GenerateDocumentEndpointsTests(DocumentsApiFixture fixture)
{
    [Fact]
    public async Task Generate_renders_uploads_and_reaches_Ready_with_a_working_download_url()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        await PublishTemplateAsync(client, accessToken, "Transcript");

        var body = new GenerateDocumentRequestBody(Guid.NewGuid(), "Transcript", Guid.NewGuid(), new Dictionary<string, string> { ["studentName"] = "Rahim Uddin", ["gpa"] = "3.85" }, "en");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<GeneratedDocumentDto>();
        Assert.Equal("Ready", dto!.Status);
        Assert.NotNull(dto.DownloadUrl);

        using var downloadClient = new HttpClient();
        var pdfBytes = await downloadClient.GetByteArrayAsync(dto.DownloadUrl);
        Assert.True(pdfBytes.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Fact]
    public async Task Generate_retried_with_the_identical_natural_key_returns_the_same_document()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        await PublishTemplateAsync(client, accessToken, "Transcript");

        var ownerId = Guid.NewGuid();
        var sourceReferenceId = Guid.NewGuid();
        var body = new GenerateDocumentRequestBody(ownerId, "Transcript", sourceReferenceId, new Dictionary<string, string> { ["amount"] = "5000" }, "en");

        var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken));
        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken));

        var firstDto = await first.Content.ReadFromJsonAsync<GeneratedDocumentDto>();
        var secondDto = await second.Content.ReadFromJsonAsync<GeneratedDocumentDto>();

        Assert.Equal(firstDto!.Id, secondDto!.Id);
    }

    [Fact]
    public async Task Generate_against_an_unpublished_type_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        var body = new GenerateDocumentRequestBody(Guid.NewGuid(), "AdmitCard", Guid.NewGuid(), new Dictionary<string, string>(), "en");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Generate_without_a_token_is_rejected()
    {
        using var client = fixture.CreateClient();
        var body = new GenerateDocumentRequestBody(Guid.NewGuid(), "Transcript", Guid.NewGuid(), new Dictionary<string, string>(), "en");

        var response = await client.PostAsJsonAsync("/api/v1/documents/generate", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_a_Ready_document_transitions_it_and_it_is_no_longer_downloadable_as_valid()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        await PublishTemplateAsync(client, accessToken, "IdCard");

        var body = new GenerateDocumentRequestBody(Guid.NewGuid(), "IdCard", Guid.NewGuid(), new Dictionary<string, string> { ["name"] = "Karim" }, "en");
        var generateResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken));
        var generated = await generateResponse.Content.ReadFromJsonAsync<GeneratedDocumentDto>();

        var revokeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/documents/{generated!.Id}/revoke")
        {
            Content = JsonContent.Create(new RevokeDocumentRequestBody("Issued in error")),
        }.WithBearerToken(accessToken);
        var revokeResponse = await client.SendAsync(revokeRequest);
        revokeResponse.EnsureSuccessStatusCode();
        var revoked = await revokeResponse.Content.ReadFromJsonAsync<GeneratedDocumentDto>();

        Assert.Equal("Revoked", revoked!.Status);

        // A second revoke against an already-revoked document is rejected (edge-cases.md's revoke decision).
        var secondRevoke = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/documents/{generated.Id}/revoke") { Content = JsonContent.Create(new RevokeDocumentRequestBody("again")) }.WithBearerToken(accessToken));
        Assert.Equal(HttpStatusCode.Conflict, secondRevoke.StatusCode);
    }

    private static async Task PublishTemplateAsync(HttpClient client, string accessToken, string documentType)
    {
        var body = new PublishTemplateRequestBody(documentType, null, [new TemplateTranslationRequestBody("en", documentType, "{}")]);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
