namespace UMS.Modules.Career.Application.Drives;

public sealed record InterviewSlotDto(Guid Id, Guid DriveId, DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity, int BookedCount, bool IsCancelled);

public sealed record DefineInterviewSlotRequest(DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity);

public sealed record DefineInterviewSlotsRequest(IReadOnlyCollection<DefineInterviewSlotRequest> Slots);

/// <summary>CAR-12/CAR-13's Api-layer request body - the calling Student's own `CareerApplication` against this Drive, whose ownership `InterviewSlotService` itself verifies against the caller's resolved `StudentId`.</summary>
public sealed record BookInterviewSlotRequest(Guid CareerApplicationId);

public sealed record CancelInterviewSlotBookingRequest(Guid CareerApplicationId);
