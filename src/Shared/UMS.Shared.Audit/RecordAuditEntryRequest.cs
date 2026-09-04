namespace UMS.Shared.Audit;

/// <summary>
/// The one shape every one of the other 17 modules uses to call <see cref="IAuditRecorder"/>
/// (AUD-1; requirement-spec.md audit §2's field list). <see cref="OccurredAt"/> is deliberately
/// not a caller-supplied field - Audit stamps it itself, from its own clock, at the moment the
/// row is actually written, so it always agrees with the monotonic ULID id's embedded timestamp
/// (design-decisions.md, "Ordering/Sequencing Mechanism").
/// </summary>
/// <param name="ActorId">
/// The acting <c>User</c>'s id (as a string - Audit depends on no other module, so it never
/// references Identity's own <c>UserId</c> type, per module-boundaries.md) when
/// <paramref name="ActorType"/> is <see cref="AuditActorType.User"/>; a well-known
/// <c>system:&lt;job-name&gt;</c> principal name (e.g. <c>"system:result-publisher"</c>) when
/// <see cref="AuditActorType.System"/> (requirement-spec.md audit §8).
/// </param>
/// <param name="IpAddress">
/// The originating request's client IP. <c>null</c> for a system/background-job actor, which has
/// no client connection to report (requirement-spec.md audit §2's example entry omits it for the
/// <c>system:result-publisher</c> case).
/// </param>
/// <param name="Application">
/// Which of the six SPAs (e.g. <c>"ums-admin-web"</c>) or <c>"system"</c> originated the request
/// (requirement-spec.md audit §2).
/// </param>
/// <param name="EntityType">
/// The aggregate name exactly as `docs/ddd/ubiquitous-language.md` defines it (e.g.
/// <c>"Grade"</c>, <c>"Payment"</c>) - never an abbreviation or a table name (requirement-spec.md
/// audit §4).
/// </param>
/// <param name="EntityId">The affected entity's own id, as a string (Audit stores every module's ids opaquely).</param>
/// <param name="Action">
/// One of <see cref="AuditActions"/>' fixed vocabulary, or another module-specific lowercase
/// snake_case verb (requirement-spec.md audit §2).
/// </param>
/// <param name="BeforeValueJson">
/// A JSON snapshot of the changed fields only (never the full row) before the mutation, or
/// <c>null</c> for a pure creation. Object-storage-backed content (a document, a generated PDF)
/// must be referenced by its metadata/id here, never inlined (requirement-spec.md audit §8).
/// </param>
/// <param name="AfterValueJson">The same shape as <paramref name="BeforeValueJson"/>, after the mutation; <c>null</c> for a pure deletion.</param>
/// <param name="CorrelationId">The request/event chain's correlation id, threaded end-to-end (ums-conventions.md, Observability).</param>
/// <param name="Reason">
/// Free text. Required (validated by <see cref="IAuditRecorder"/>'s implementation) whenever
/// <paramref name="Action"/> is <see cref="AuditActions.Reject"/> or <see cref="AuditActions.Revoke"/>,
/// or when the caller explicitly marks the mutation as a correction/reversal via
/// <paramref name="IsCorrection"/> - optional otherwise (requirement-spec.md audit §4).
/// </param>
/// <param name="IsCorrection">
/// Set by the calling module when this entry documents overturning a prior decision that isn't
/// already one of the two actions treated as inherently reversal-shaped (e.g. a grade correction,
/// which is otherwise a plain <see cref="AuditActions.Update"/>) - see <see cref="Reason"/>.
/// </param>
/// <param name="OrganizationScopeId">
/// An opaque reference to the affected entity's own owning <c>OrganizationNode</c> (Faculty/
/// Department/Program - Organization module, Flow #6), captured at write time so a later read can
/// be scoped to the caller's own ScopeGrant (requirement-spec.md audit §2/§4: "using each source
/// entity's own organizational-scope metadata passed in at write time"). <c>null</c> when the
/// entity has no organizational scope of its own.
/// </param>
public sealed record RecordAuditEntryRequest(
    string ActorId,
    AuditActorType ActorType,
    string? IpAddress,
    string Application,
    string EntityType,
    string EntityId,
    string Action,
    string? BeforeValueJson,
    string? AfterValueJson,
    string CorrelationId,
    string? Reason = null,
    bool IsCorrection = false,
    Guid? OrganizationScopeId = null);
