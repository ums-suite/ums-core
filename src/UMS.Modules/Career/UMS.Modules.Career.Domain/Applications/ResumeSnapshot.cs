namespace UMS.Modules.Career.Domain.Applications;

/// <summary>
/// design-decisions.md "ResumeProfile Snapshot-at-Submission Immutability": a point-in-time COPY of a
/// `ResumeProfile`'s reference/metadata, taken the moment a `CareerApplication` is submitted - never a
/// live foreign key. Later edits to, or deletion of, the source `ResumeProfile` never retroactively
/// change what this record shows a reviewer (requirement-spec.md §4, §2.7 last bullet).
/// </summary>
public sealed record ResumeSnapshot(Guid ResumeProfileId, Guid ArtifactId, string FileName, DateTimeOffset SnapshotAt);
