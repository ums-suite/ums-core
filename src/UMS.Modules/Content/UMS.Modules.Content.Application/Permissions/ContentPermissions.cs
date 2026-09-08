namespace UMS.Modules.Content.Application.Permissions;

/// <summary>
/// requirement-spec.md §7, verified against Identity's actual catalog validator (at least 3
/// dot-separated segments, each lowercase letters/digits only - no underscore; see
/// ums-core-gotchas memory).
/// </summary>
public static class ContentPermissions
{
    public const string NoticeWrite = "content.notice.write";
    public const string NoticePublish = "content.notice.publish";
    public const string NoticeArchive = "content.notice.archive";

    /// <summary>Gates the authenticated Student/Faculty/Admin feed (CNT-12) - granted to every Student/Faculty/Admin-facing Role by Identity's own seed data, never to an anonymous caller.</summary>
    public const string NoticeRead = "content.notice.read";

    public const string EventWrite = "content.event.write";
    public const string BannerWrite = "content.banner.write";
    public const string BannerPublish = "content.banner.publish";
    public const string HomepageWrite = "content.homepage.write";
    public const string DownloadWrite = "content.download.write";
}
