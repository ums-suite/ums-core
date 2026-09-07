namespace UMS.Modules.Finance.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="FinanceServiceFixture"/> (one real Postgres container) across every test class in this suite - mirrors every other module's own test collection definition exactly.</summary>
[CollectionDefinition(Name)]
public sealed class FinanceApiTestCollectionDefinition : ICollectionFixture<FinanceServiceFixture>
{
    public const string Name = "FinanceApi";
}
