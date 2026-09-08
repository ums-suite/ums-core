namespace UMS.Modules.Library.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class LibraryApiTestCollectionDefinition : ICollectionFixture<LibraryServiceFixture>
{
    public const string Name = "LibraryApi";
}
