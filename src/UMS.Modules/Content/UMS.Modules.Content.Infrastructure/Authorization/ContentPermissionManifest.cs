using UMS.Modules.Content.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Content.Infrastructure.Authorization;

internal sealed class ContentPermissionManifest : IPermissionManifest
{
    public string OwningModule => "content";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(ContentPermissions.NoticeWrite, "Create/edit Notice content (Content editor/Admin)."),
        new(ContentPermissions.NoticePublish, "Manually publish or archive a Notice, bypassing the schedule (Content editor/Admin)."),
        new(ContentPermissions.NoticeArchive, "Manually archive a Published Notice (Content editor/Admin)."),
        new(ContentPermissions.NoticeRead, "Read audience-scoped Notices/Events via the authenticated Student/Faculty/Admin feed."),
        new(ContentPermissions.EventWrite, "Create/edit calendar Events (Content editor/Admin)."),
        new(ContentPermissions.BannerWrite, "Create/edit homepage Banners (Content editor/Admin)."),
        new(ContentPermissions.BannerPublish, "Manually publish or archive a Banner, bypassing the schedule (Content editor/Admin)."),
        new(ContentPermissions.HomepageWrite, "Reorder/toggle homepage sections (Content editor/Admin)."),
        new(ContentPermissions.DownloadWrite, "Upload/manage Download resource metadata (Content editor/Admin)."),
    ];
}
