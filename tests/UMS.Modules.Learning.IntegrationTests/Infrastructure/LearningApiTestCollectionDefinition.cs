namespace UMS.Modules.Learning.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="LearningApiFixture"/> across every test class in this suite - mirrors <c>AcademicApiTestCollectionDefinition</c> exactly.</summary>
[CollectionDefinition(Name)]
public sealed class LearningApiTestCollectionDefinition : ICollectionFixture<LearningApiFixture>
{
    public const string Name = "LearningApi";
}
