namespace UMS.Modules.Reporting.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="ReportingServiceFixture"/> (one real Postgres + Redis container pair) across every test class in this suite - mirrors every other module's own test collection definition exactly.</summary>
[CollectionDefinition(Name)]
public sealed class ReportingTestCollectionDefinition : ICollectionFixture<ReportingServiceFixture>
{
    public const string Name = "ReportingApi";
}
