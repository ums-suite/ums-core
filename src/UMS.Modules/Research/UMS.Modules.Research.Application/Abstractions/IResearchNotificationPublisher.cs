namespace UMS.Modules.Research.Application.Abstractions;

/// <summary>Research's own local port, adapted onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> in Infrastructure - mirrors every other module's own <c>INotificationRequestPublisher</c> exactly.</summary>
public interface IResearchNotificationPublisher
{
    public Task PublishAsync(ResearchNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// requirement-spec.md §7/§11: <c>GrantFunded</c>/<c>GrantClosed</c>/<c>GrantReported</c> -&gt; PI/Co-I
/// notification; <c>GrantPiReassignmentRequired</c> -&gt; Admin/Research-Office alert.
///
/// <para>
/// <b>Known, inherited gap</b> (documented rather than glossed over - mirrors Library's own
/// <c>BorrowerContextService.ResolveIdentityUserIdAsync</c> remark for the identical situation):
/// <c>UMS.Shared.Faculty.FacultyMemberSummary</c> carries no Identity <c>UserId</c> field today, so
/// a PI/Co-Investigator notification's <see cref="RecipientId"/> falls back to the FacultyMemberId
/// itself as the "best available identifier" - not a gap this module invents, one it inherits from
/// the shared contract's own current shape.
/// </para>
/// </summary>
public sealed record ResearchNotificationRequest(string EventType, string SourceEntityId, Guid RecipientId, IReadOnlyDictionary<string, string?> MergeFields);
