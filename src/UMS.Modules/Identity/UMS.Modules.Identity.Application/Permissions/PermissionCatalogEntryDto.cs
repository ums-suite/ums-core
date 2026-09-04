namespace UMS.Modules.Identity.Application.Permissions;

public sealed record PermissionCatalogEntryDto(string Key, string OwningModule, string Description, DateTimeOffset RegisteredAt);
