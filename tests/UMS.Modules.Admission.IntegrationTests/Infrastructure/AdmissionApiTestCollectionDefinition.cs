namespace UMS.Modules.Admission.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="AdmissionServiceFixture"/> (one real Postgres + Redis container pair) across every test class in this suite - mirrors every other module's own test collection definition exactly.</summary>
[CollectionDefinition(Name)]
public sealed class AdmissionApiTestCollectionDefinition : ICollectionFixture<AdmissionServiceFixture>
{
    public const string Name = "AdmissionApi";
}
