namespace UMS.Modules.Faculty.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="FacultyApiFixture"/> across every test class in this suite - mirrors <c>OrganizationApiTestCollectionDefinition</c> exactly.</summary>
[CollectionDefinition(Name)]
public sealed class FacultyApiTestCollectionDefinition : ICollectionFixture<FacultyApiFixture>
{
    public const string Name = "FacultyApi";
}
