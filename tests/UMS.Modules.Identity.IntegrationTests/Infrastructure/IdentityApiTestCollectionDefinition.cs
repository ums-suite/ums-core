namespace UMS.Modules.Identity.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="IdentityApiFixture"/> (one Postgres + one Redis container) across every test class in this suite - starting a fresh container pair per class would dominate suite runtime.</summary>
[CollectionDefinition(Name)]
public sealed class IdentityApiTestCollectionDefinition : ICollectionFixture<IdentityApiFixture>
{
    public const string Name = "IdentityApi";
}
