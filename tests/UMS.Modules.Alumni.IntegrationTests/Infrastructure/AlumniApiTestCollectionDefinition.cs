namespace UMS.Modules.Alumni.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class AlumniApiTestCollectionDefinition : ICollectionFixture<AlumniServiceFixture>
{
    public const string Name = "AlumniApi";
}
