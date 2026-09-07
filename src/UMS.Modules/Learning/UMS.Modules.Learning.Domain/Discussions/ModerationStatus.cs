namespace UMS.Modules.Learning.Domain.Discussions;

/// <summary>requirement-spec.md learning §4: moderation is a STATE TRANSITION, never a delete - a removed post's content and authorship are retained (edge-cases.md, "A Student posts something requiring moderation removal").</summary>
public enum ModerationStatus
{
    Visible,
    Removed,
}
