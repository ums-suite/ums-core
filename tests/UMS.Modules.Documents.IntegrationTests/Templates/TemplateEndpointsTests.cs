using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Templates;
using UMS.Modules.Documents.IntegrationTests.Infrastructure;

namespace UMS.Modules.Documents.IntegrationTests.Templates;

/// <summary>
/// DOC-1: publish/read against the real running API + Postgres.
///
/// <para>
/// Every test class in this suite shares one <see cref="DocumentsApiFixture"/> (one collection,
/// one Postgres database - xUnit runs every test within a collection sequentially, never in
/// parallel with each other, but does not guarantee cross-class ordering). Only six
/// <c>DocumentType</c> values exist platform-wide, so a test asserting an absolute fact ("this is
/// version 1", "nothing has ever been published for this type") needs a type no other test in the
/// suite ever touches - <c>MeritList</c> and <c>Receipt</c> are reserved for exactly that here;
/// every other test class's own comments note which types are safe to share (any test only
/// asserting "a template exists", never an absolute version/existence fact).
/// </para>
/// </summary>
[Collection(DocumentsApiTestCollectionDefinition.Name)]
public class TemplateEndpointsTests(DocumentsApiFixture fixture)
{
    [Fact]
    public async Task Publishing_a_template_creates_version_1_and_it_becomes_current()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        // MeritList: reserved exclusively for this test suite-wide - see the class remarks.
        var body = new PublishTemplateRequestBody(
            "MeritList",
            null,
            [new TemplateTranslationRequestBody("en", "Merit List", "{}"), new TemplateTranslationRequestBody("bn", "মেধা তালিকা", "{}")]);

        var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var postResponse = await client.SendAsync(postRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<DocumentTemplateDto>();
        Assert.Equal(1, created!.Version);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/documents/templates/current?documentType=MeritList").WithBearerToken(accessToken);
        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        var current = await getResponse.Content.ReadFromJsonAsync<DocumentTemplateDto>();

        Assert.Equal(created.Id, current!.Id);
    }

    [Fact]
    public async Task Publishing_a_second_time_for_the_same_type_bumps_the_version_without_mutating_the_first()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        var documentType = "Certificate";

        var first = await PublishAsync(client, accessToken, documentType, "Certificate v1");
        var second = await PublishAsync(client, accessToken, documentType, "Certificate v2");

        // A relative assertion, not an absolute "starts at 1" one - Certificate is not otherwise
        // reserved suite-wide, so another test may have already published one before this runs.
        Assert.Equal(first.Version + 1, second.Version);
        Assert.NotEqual(first.Id, second.Id);

        var currentRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/documents/templates/current?documentType={documentType}").WithBearerToken(accessToken);
        var currentResponse = await client.SendAsync(currentRequest);
        var current = await currentResponse.Content.ReadFromJsonAsync<DocumentTemplateDto>();

        Assert.Equal(second.Id, current!.Id);
    }

    [Fact]
    public async Task Publishing_without_an_English_translation_is_rejected()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        var body = new PublishTemplateRequestBody("IdCard", null, [new TemplateTranslationRequestBody("bn", "পরিচয়পত্র", "{}")]);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Requesting_the_current_template_for_a_type_with_none_published_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/documents/templates/current?documentType=Receipt").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<DocumentTemplateDto> PublishAsync(HttpClient client, string accessToken, string documentType, string title)
    {
        var body = new PublishTemplateRequestBody(documentType, null, [new TemplateTranslationRequestBody("en", title, "{}")]);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentTemplateDto>())!;
    }
}
