namespace UMS.Modules.Career.Application.Drives;

public sealed record InterviewSlotDto(Guid Id, Guid DriveId, DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity, int BookedCount, bool IsCancelled);

public sealed record DefineInterviewSlotRequest(DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity);

public sealed record DefineInterviewSlotsRequest(IReadOnlyCollection<DefineInterviewSlotRequest> Slots);
