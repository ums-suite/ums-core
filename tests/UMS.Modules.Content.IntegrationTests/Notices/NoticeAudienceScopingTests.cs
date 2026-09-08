using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Application.Permissions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.IntegrationTests.Infrastructure;

namespace UMS.Modules.Content.IntegrationTests.Notices;

/// <summary>
/// CNT-3: requirement-spec.md §4 "Audience scoping is enforced server-side on every read ... a
/// Student-scoped notice must never be returned by the public unauthenticated endpoint even if the
/// caller guesses its id." Reuses <see cref="ContentServiceFixture.ScopeGrants"/> (the
/// <c>IScopeGrantDirectory</c> fake) directly, never a parallel scoping model.
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class NoticeAudienceScopingTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task Public_unauthenticated_read_never_returns_a_Student_scoped_notice_even_by_guessed_id()
    {
        var organizationNodeId = Guid.NewGuid();
        Guid noticeId;
        uint version;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var created = await service.CreateAsync("Department Notice", "Body", ContentAudience.Student, organizationNodeId, isUrgent: false, Guid.NewGuid());
            noticeId = created.Value.Id;
            version = created.Value.Version;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
            await service.PublishAsync(noticeId, audit, version);
        }

        using var readScope = fixture.Services.CreateScope();
        var reader = readScope.ServiceProvider.GetRequiredService<NoticeService>();
        var anonymousRead = await reader.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: null);

        Assert.True(anonymousRead.IsFailure);
        Assert.Equal("notice.not_found", anonymousRead.Error!.Code);
    }

    [Fact]
    public async Task ListForAudienceAsync_excludes_a_Department_scoped_notice_until_the_caller_holds_the_scope_grant()
    {
        var organizationNodeId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        Guid noticeId;
        uint version;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var created = await service.CreateAsync("Department Notice", "Body", ContentAudience.Student, organizationNodeId, isUrgent: false, Guid.NewGuid());
            noticeId = created.Value.Id;
            version = created.Value.Version;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
            await service.PublishAsync(noticeId, audit, version);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var beforeGrant = await service.ListForAudienceAsync(ContentAudience.Student, studentUserId, preferredLanguage: null, skip: 0, take: 50);
            Assert.DoesNotContain(beforeGrant.Items, n => n.Id == noticeId);
        }

        fixture.ScopeGrants.Grant(studentUserId, ContentPermissions.NoticeRead, organizationNodeId);

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var afterGrant = await service.ListForAudienceAsync(ContentAudience.Student, studentUserId, preferredLanguage: null, skip: 0, take: 50);
            Assert.Contains(afterGrant.Items, n => n.Id == noticeId);
        }
    }
}
