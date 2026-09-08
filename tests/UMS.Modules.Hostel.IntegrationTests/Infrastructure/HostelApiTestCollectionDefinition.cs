namespace UMS.Modules.Hostel.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class HostelApiTestCollectionDefinition : ICollectionFixture<HostelServiceFixture>
{
    public const string Name = "HostelApi";
}
