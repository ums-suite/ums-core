using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IMentorshipOptInRepository
{
    public Task<MentorshipOptIn?> GetAsync(Guid personId, MentorshipRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// design-decisions.md "Mentor-Capacity Enforcement Mechanism": the ONLY mutation path for
    /// <c>active_count</c> - an atomic <c>UPDATE ... WHERE active_count &lt; capacity_limit</c>.
    /// Returns <see langword="true"/> only if the conditional update actually affected a row (a slot
    /// was genuinely claimed); a plain <c>ExecuteSqlInterpolatedAsync</c> single-statement UPDATE
    /// suffices here (no multi-step lock-then-read is needed - ums-core-gotchas: a
    /// ComplexProperty-mapped value object would rule that shortcut out, but MentorshipOptIn's
    /// capacity columns are plain scalars).
    /// </summary>
    public Task<bool> TryClaimMentorCapacityAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default);

    /// <summary>Releases a previously-claimed slot on <see cref="Domain.Events.MentorshipMatchEnded"/> or a rejected still-Proposed match (design-decisions.md: "both Proposed and Active matches count against capacity").</summary>
    public Task ReleaseMentorCapacityAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default);

    public void Add(MentorshipOptIn optIn);
}
