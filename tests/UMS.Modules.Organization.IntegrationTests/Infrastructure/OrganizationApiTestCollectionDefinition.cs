namespace UMS.Modules.Organization.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="OrganizationApiFixture"/> (one Postgres + one Redis container) across every test class in this suite - mirrors <c>IdentityApiTestCollectionDefinition</c> exactly.</summary>
[CollectionDefinition(Name)]
public sealed class OrganizationApiTestCollectionDefinition : ICollectionFixture<OrganizationApiFixture>
{
    public const string Name = "OrganizationApi";
}
