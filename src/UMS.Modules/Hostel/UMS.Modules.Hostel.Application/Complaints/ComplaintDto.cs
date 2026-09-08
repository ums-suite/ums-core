namespace UMS.Modules.Hostel.Application.Complaints;

public sealed record ComplaintDto(
    Guid Id,
    Guid StudentId,
    Guid AllocationId,
    string Category,
    string Description,
    string Status,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record SubmitComplaintRequest(Guid AllocationId, string Category, string Description, string? IdempotencyKey);

public sealed record PatchComplaintRequest(string Status, string? ResolutionNote);
