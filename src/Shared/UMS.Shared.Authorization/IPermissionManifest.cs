namespace UMS.Shared.Authorization;

/// <summary>
/// Implemented once per module that owns any Permission strings. Registered into DI as
/// <c>IPermissionManifest</c> by that module's own composition-root extension; Identity's catalog
/// synchronizer collects every registered manifest (<c>IEnumerable&lt;IPermissionManifest&gt;</c>)
/// at startup and upserts their entries into the shared catalog table it owns (identity §2, §6
/// `GET /permissions`). Living here (rather than inside
/// <c>UMS.Modules.Identity.*</c>) is what lets a future module register its own permissions
/// without taking a forbidden dependency on Identity's Domain/Application/Infrastructure
/// internals (module-boundaries.md, ADR-0002) - only this already-shared package.
/// </summary>
public interface IPermissionManifest
{
    public string OwningModule { get; }

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; }
}
