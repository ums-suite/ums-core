namespace UMS.Modules.Research.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ResearchApiTestCollectionDefinition : ICollectionFixture<ResearchServiceFixture>
{
    public const string Name = "ResearchApi";
}
