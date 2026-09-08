using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Downloads;
using UMS.Modules.Content.IntegrationTests.Infrastructure;

namespace UMS.Modules.Content.IntegrationTests.Downloads;

/// <summary>CNT-11: requirement-spec.md §2.6 - Content never stores the blob; a DownloadResource cannot be created against an artifact that hasn't finished uploading (<c>IUploadedArtifactRequester.ConfirmAsync</c> not yet `Ready`).</summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class DownloadResourceArtifactReadinessTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task Create_rejects_an_artifact_that_has_not_finished_uploading()
    {
        var artifactId = Guid.NewGuid();
        fixture.UploadedArtifacts.MarkNotReady(artifactId);

        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DownloadResourceService>();

        var result = await service.CreateAsync("Prospectus", "Forms", artifactId, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("download.artifact_not_ready", result.Error!.Code);
    }

    [Fact]
    public async Task Create_succeeds_once_the_artifact_is_Ready()
    {
        var artifactId = Guid.NewGuid();

        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DownloadResourceService>();

        var result = await service.CreateAsync("Prospectus", "Forms", artifactId, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(artifactId, result.Value.ArtifactId);
    }
}
