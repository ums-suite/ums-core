namespace UMS.Modules.Career.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class CareerApiTestCollectionDefinition : ICollectionFixture<CareerServiceFixture>
{
    public const string Name = "CareerApi";
}
