namespace UMS.Modules.Student.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="StudentApiFixture"/> across every test class in this suite - mirrors <c>FacultyApiTestCollectionDefinition</c> exactly.</summary>
[CollectionDefinition(Name)]
public sealed class StudentApiTestCollectionDefinition : ICollectionFixture<StudentApiFixture>
{
    public const string Name = "StudentApi";
}
