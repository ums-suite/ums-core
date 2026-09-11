using UMS.Modules.Career.Domain.Common;
using UMS.Modules.Career.Domain.Events;

namespace UMS.Modules.Career.Domain.ResumeProfiles;

/// <summary>
/// CAR-5: a Student-owned, named resume version (requirement-spec.md §2.7, §3, §9 multi-version
/// decision). References one uploaded PDF via the object-storage-metadata pattern
/// (<c>UMS.Shared.Documents.IUploadedArtifactRequester</c>, requirement-spec.md §7.1) - stores only
/// the returned <c>artifactId</c>, never file bytes.
///
/// <para>
/// design-decisions.md "ResumeProfile Snapshot-at-Submission Immutability": THIS row is the live,
/// editable side of that invariant - a <see cref="Applications.CareerApplication"/> never holds a
/// live reference to this entity, only a point-in-time copy of <see cref="ArtifactId"/>/
/// <see cref="FileName"/> taken at submission (see <c>ResumeSnapshot</c>'s own remarks). Editing or
/// deleting THIS row therefore never retroactively changes any prior snapshot.
/// </para>
/// </summary>
public sealed class ResumeProfile : AggregateRoot<ResumeProfileId>
{
    private ResumeProfile()
    {
    }

    private ResumeProfile(ResumeProfileId id, Guid studentId, string label, Guid artifactId, string fileName, bool isDefault, DateTimeOffset createdAt)
    {
        Id = id;
        StudentId = studentId;
        Label = label;
        ArtifactId = artifactId;
        FileName = fileName;
        IsDefault = isDefault;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid StudentId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    /// <summary>The `UploadedArtifact` id Documents returned after its own synchronous, `Ready`-gated upload confirmation (edge-cases.md "ResumeProfile submission racing storage confirmation").</summary>
    public Guid ArtifactId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    /// <summary>requirement-spec.md §2.7: exactly one `ResumeProfile` per Student is flagged default at any time - enforced by a partial unique index (see `ResumeProfileConfiguration`), not merely in-process.</summary>
    public bool IsDefault { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ResumeProfile Create(Guid studentId, string label, Guid artifactId, string fileName, bool isDefault, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A ResumeProfile's label is required.", nameof(label));
        }

        var profile = new ResumeProfile(ResumeProfileId.New(), studentId, label.Trim(), artifactId, fileName.Trim(), isDefault, now);
        profile.Raise(new ResumeProfileUpdated(profile.Id.Value, studentId, now));
        return profile;
    }

    /// <summary>requirement-spec.md §2.7 last bullet / §4 snapshot invariant: a NEW upload or field edit never retroactively changes an already-taken snapshot - this only mutates the LIVE row.</summary>
    public void Update(string label, Guid? newArtifactId, string? newFileName, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot edit a deleted ResumeProfile.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A ResumeProfile's label is required.", nameof(label));
        }

        Label = label.Trim();
        if (newArtifactId is { } artifactId)
        {
            ArtifactId = artifactId;
            FileName = newFileName?.Trim() ?? FileName;
        }

        UpdatedAt = now;
        Raise(new ResumeProfileUpdated(Id.Value, StudentId, now));
    }

    public void MarkAsDefault() => IsDefault = true;

    public void UnmarkAsDefault() => IsDefault = false;

    /// <summary>edge-cases.md "A Student deletes a ResumeProfile that a past, still-viewable CareerApplication snapshot references" - a soft delete: the row (and its id) must remain resolvable for FK integrity even though `ResumeProfileService`'s own list/read no longer surfaces it, since a submitted snapshot never dereferences this row live anyway (it copied the reference at submission time).</summary>
    public void Delete()
    {
        IsDeleted = true;
        IsDefault = false;
    }
}
