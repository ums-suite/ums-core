namespace UMS.Modules.Student.Domain.StudentRequests;

/// <summary>requirement-spec.md student §2: "Submitted -> UnderReview -> Approved/Rejected -> Fulfilled." <see cref="UnderReview"/> has no dedicated transition endpoint in this build (STU-9..14 do not name one) but is retained as a legal, reachable-in-principle state - it, together with <see cref="Submitted"/>, is what design-decisions.md's dedup partial-unique-index filters on as "open."</summary>
public enum StudentRequestStatus
{
    Submitted = 0,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3,
    Fulfilled = 4,
}
