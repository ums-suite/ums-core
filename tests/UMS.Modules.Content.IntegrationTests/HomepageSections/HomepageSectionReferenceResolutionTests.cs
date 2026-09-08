using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.HomepageSections;
using UMS.Modules.Content.IntegrationTests.Infrastructure;

namespace UMS.Modules.Content.IntegrationTests.HomepageSections;

/// <summary>
/// CNT-10/edge-cases.md "Organization reference later deleted/deactivated": a
/// <c>ReferenceOrganizationNodeId</c> that doesn't resolve degrades to
/// <see cref="HomepageSectionDto.ReferenceAvailable"/> = <see langword="false"/> at read time,
/// never a thrown exception.
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class HomepageSectionReferenceResolutionTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task Reference_to_a_missing_Organization_node_degrades_to_unavailable_rather_than_throwing()
    {
        var missingNodeId = Guid.NewGuid();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<HomepageSectionService>();

        var created = await service.CreateAsync("featured-programs", "Featured Programs", 1, true, missingNodeId);

        Assert.True(created.IsSuccess);
        Assert.False(created.Value.ReferenceAvailable);
    }

    [Fact]
    public async Task Reference_to_an_existing_Organization_node_resolves_as_available()
    {
        var existingNodeId = Guid.NewGuid();
        fixture.OrganizationNodes.Register(existingNodeId);

        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<HomepageSectionService>();

        var created = await service.CreateAsync("featured-programs-2", "Featured Programs", 2, true, existingNodeId);

        Assert.True(created.IsSuccess);
        Assert.True(created.Value.ReferenceAvailable);
    }
}
