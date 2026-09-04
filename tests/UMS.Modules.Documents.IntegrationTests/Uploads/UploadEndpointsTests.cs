using System.Net;
using System.Net.Http.Json;
using System.Text;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Uploads;
using UMS.Modules.Documents.IntegrationTests.Infrastructure;

namespace UMS.Modules.Documents.IntegrationTests.Uploads;

/// <summary>requirement-spec.md documents §2 Uploaded Artifact Storage: presigned upload -&gt; client uploads directly to object storage -&gt; confirm -&gt; Ready, against real MinIO (a real object-storage round trip).</summary>
[Collection(DocumentsApiTestCollectionDefinition.Name)]
public class UploadEndpointsTests(DocumentsApiFixture fixture)
{
    [Fact]
    public async Task Requesting_confirming_and_downloading_an_uploaded_artifact_round_trips_through_real_object_storage()
    {
        using var client = fixture.CreateClient();
        var (userId, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        var requestBody = new RequestUploadRequestBody(userId, "ResumeProfile", "application/pdf");
        var requestResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/uploads") { Content = JsonContent.Create(requestBody) }.WithBearerToken(accessToken));
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        var created = await requestResponse.Content.ReadFromJsonAsync<UploadedArtifactDto>();
        Assert.Equal("PendingUpload", created!.Status);
        Assert.NotNull(created.UploadUrl);

        // The client uploads directly to object storage using the presigned URL - Documents never
        // proxies the byte stream itself (§2).
        var fileContent = Encoding.UTF8.GetBytes("%PDF-1.4 fake resume content");
        using var uploadClient = new HttpClient();
        using var putContent = new ByteArrayContent(fileContent);
        putContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        var putResponse = await uploadClient.PutAsync(created.UploadUrl, putContent);
        Assert.True(putResponse.IsSuccessStatusCode, $"Direct-to-storage upload failed: {putResponse.StatusCode}");

        var confirmResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/documents/uploads/{created.Id}/confirm").WithBearerToken(accessToken));
        confirmResponse.EnsureSuccessStatusCode();
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<UploadedArtifactDto>();
        Assert.Equal("Ready", confirmed!.Status);
        Assert.Equal(fileContent.Length, confirmed.SizeBytes);

        var getResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/documents/uploads/{created.Id}").WithBearerToken(accessToken));
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<UploadedArtifactDto>();
        Assert.NotNull(fetched!.DownloadUrl);

        using var downloadClient = new HttpClient();
        var downloaded = await downloadClient.GetByteArrayAsync(fetched.DownloadUrl);
        Assert.Equal(fileContent, downloaded);
    }

    [Fact]
    public async Task Confirming_an_upload_that_never_happened_marks_it_Failed_not_Ready()
    {
        using var client = fixture.CreateClient();
        var (userId, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        var requestBody = new RequestUploadRequestBody(userId, "ResumeProfile", "application/pdf");
        var requestResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/uploads") { Content = JsonContent.Create(requestBody) }.WithBearerToken(accessToken));
        var created = await requestResponse.Content.ReadFromJsonAsync<UploadedArtifactDto>();

        // Confirm without ever PUTting anything to the presigned URL.
        var confirmResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/documents/uploads/{created!.Id}/confirm").WithBearerToken(accessToken));
        confirmResponse.EnsureSuccessStatusCode();
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<UploadedArtifactDto>();

        Assert.Equal("Failed", confirmed!.Status);
    }
}
