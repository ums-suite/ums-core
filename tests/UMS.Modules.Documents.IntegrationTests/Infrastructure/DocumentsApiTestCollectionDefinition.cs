namespace UMS.Modules.Documents.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="DocumentsApiFixture"/> (one Postgres + one Redis + one MinIO container) across every test class in this suite.</summary>
[CollectionDefinition(Name)]
public sealed class DocumentsApiTestCollectionDefinition : ICollectionFixture<DocumentsApiFixture>
{
    public const string Name = "DocumentsApi";
}
