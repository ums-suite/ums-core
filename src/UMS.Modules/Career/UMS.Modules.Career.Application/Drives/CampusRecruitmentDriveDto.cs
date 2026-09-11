namespace UMS.Modules.Career.Application.Drives;

public sealed record CampusRecruitmentDriveDto(
    Guid Id,
    Guid EmployerProfileId,
    string Title,
    Guid VenueRoomId,
    DateTimeOffset ScheduledDate,
    DateTimeOffset RegistrationOpensAt,
    DateTimeOffset RegistrationClosesAt,
    string Status,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record CreateDriveRequest(Guid EmployerProfileId, string Title, Guid VenueRoomId, DateTimeOffset ScheduledDate, DateTimeOffset RegistrationOpensAt, DateTimeOffset RegistrationClosesAt);

public sealed record EditDriveRequest(string Title, Guid VenueRoomId, DateTimeOffset ScheduledDate, DateTimeOffset RegistrationOpensAt, DateTimeOffset RegistrationClosesAt, uint Version);

public sealed record CancelDriveRequest(string Reason, uint Version);
