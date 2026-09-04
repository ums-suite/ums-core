namespace UMS.Modules.Notifications.IntegrationTests.Infrastructure;

/// <summary>Shares one <see cref="NotificationsApiFixture"/> (one Postgres + one Redis container) across every test class in this suite - mirrors Audit's own <c>AuditApiTestCollectionDefinition</c>.</summary>
[CollectionDefinition(Name)]
public sealed class NotificationsApiTestCollectionDefinition : ICollectionFixture<NotificationsApiFixture>
{
    public const string Name = "NotificationsApi";
}
