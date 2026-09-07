namespace UMS.Modules.Academic.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="AcademicApiFixture"/> across every test class in this suite - mirrors <c>StudentApiTestCollectionDefinition</c> exactly.</summary>
[CollectionDefinition(Name)]
public sealed class AcademicApiTestCollectionDefinition : ICollectionFixture<AcademicApiFixture>
{
    public const string Name = "AcademicApi";
}
