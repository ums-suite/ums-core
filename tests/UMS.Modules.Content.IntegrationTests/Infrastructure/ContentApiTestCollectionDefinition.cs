namespace UMS.Modules.Content.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ContentApiTestCollectionDefinition : ICollectionFixture<ContentServiceFixture>
{
    public const string Name = "ContentApi";
}
