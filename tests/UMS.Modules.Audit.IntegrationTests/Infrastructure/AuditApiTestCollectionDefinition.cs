namespace UMS.Modules.Audit.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="AuditApiFixture"/> (one Postgres + one Redis container) across every test class in this suite.</summary>
[CollectionDefinition(Name)]
public sealed class AuditApiTestCollectionDefinition : ICollectionFixture<AuditApiFixture>
{
    public const string Name = "AuditApi";
}
