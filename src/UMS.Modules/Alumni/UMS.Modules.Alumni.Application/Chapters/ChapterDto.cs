namespace UMS.Modules.Alumni.Application.Chapters;

public sealed record ChapterDto(Guid Id, string Name, string? Description, string? Region, int MemberCount, DateTimeOffset CreatedAt);

public sealed record CreateChapterRequest(string Name, string? Description, string? Region);
