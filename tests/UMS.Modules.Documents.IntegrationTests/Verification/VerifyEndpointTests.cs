using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Application.Templates;
using UMS.Modules.Documents.Application.Verification;
using UMS.Modules.Documents.IntegrationTests.Infrastructure;

namespace UMS.Modules.Documents.IntegrationTests.Verification;

/// <summary>
/// DOC-9: the one deliberately public, unauthenticated endpoint - requirement-spec.md documents
/// §2/§4/§8. Uses "Transcript"/"IdCard" (the shared-ok pool - see
/// <c>TemplateEndpointsTests</c>' own remarks on why only six <c>DocumentType</c> values exist
/// platform-wide and how this suite partitions them).
/// </summary>
[Collection(DocumentsApiTestCollectionDefinition.Name)]
public class VerifyEndpointTests(DocumentsApiFixture fixture)
{
    [Fact]
    public async Task Verify_a_Ready_document_returns_valid_true_with_no_authentication()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        await PublishTemplateAsync(client, accessToken, "Transcript");

        var generateBody = new GenerateDocumentRequestBody(Guid.NewGuid(), "Transcript", Guid.NewGuid(), new Dictionary<string, string> { ["name"] = "Fatima" }, "en");
        var generateResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(generateBody) }.WithBearerToken(accessToken));
        var generated = await generateResponse.Content.ReadFromJsonAsync<GeneratedDocumentDto>();

        using var anonymousClient = fixture.CreateClient();
        var verifyResponse = await anonymousClient.GetAsync($"/api/v1/documents/verify/{generated!.DigitalVerificationId}");
        verifyResponse.EnsureSuccessStatusCode();
        var verification = await verifyResponse.Content.ReadFromJsonAsync<DocumentVerificationDto>();

        Assert.True(verification!.IsValid);
        Assert.Equal("Ready", verification.Status);
    }

    [Fact]
    public async Task Verify_a_revoked_document_returns_its_true_status_not_a_bare_404()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        await PublishTemplateAsync(client, accessToken, "IdCard");

        var generateBody = new GenerateDocumentRequestBody(Guid.NewGuid(), "IdCard", Guid.NewGuid(), new Dictionary<string, string> { ["seat"] = "A1" }, "en");
        var generateResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate") { Content = JsonContent.Create(generateBody) }.WithBearerToken(accessToken));
        var generated = await generateResponse.Content.ReadFromJsonAsync<GeneratedDocumentDto>();

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/documents/{generated!.Id}/revoke") { Content = JsonContent.Create(new RevokeDocumentRequestBody("Duplicate issuance")) }.WithBearerToken(accessToken));

        using var anonymousClient = fixture.CreateClient();
        var verifyResponse = await anonymousClient.GetAsync($"/api/v1/documents/verify/{generated.DigitalVerificationId}");

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verification = await verifyResponse.Content.ReadFromJsonAsync<DocumentVerificationDto>();
        Assert.False(verification!.IsValid);
        Assert.Equal("Revoked", verification.Status);
        Assert.Contains("revoked", verification.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_an_id_that_was_never_issued_returns_404()
    {
        using var anonymousClient = fixture.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/v1/documents/verify/NEVER-ISSUED-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task PublishTemplateAsync(HttpClient client, string accessToken, string documentType)
    {
        var body = new PublishTemplateRequestBody(documentType, null, [new TemplateTranslationRequestBody("en", documentType, "{}")]);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken));
        response.EnsureSuccessStatusCode();
    }
}
