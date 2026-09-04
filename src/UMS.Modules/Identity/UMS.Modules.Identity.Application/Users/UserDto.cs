namespace UMS.Modules.Identity.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    string? Mobile,
    string? UniversityId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ProvisionUserRequest(
    string Username,
    string Email,
    string GivenName,
    string FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string? Mobile,
    string? UniversityId,
    string Password);

public sealed record UserListPage(IReadOnlyList<UserDto> Items, int TotalCount, int Skip, int Take);
