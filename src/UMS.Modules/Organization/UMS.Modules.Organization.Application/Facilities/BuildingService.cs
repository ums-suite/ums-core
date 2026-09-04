using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Facilities;

/// <summary>
/// ORG-7: Building CRUD (requirement-spec.md organization §2/§3/§6). Hard delete is not literally
/// named in §6's own API table, but is explicitly re-authorized by design-decisions.md
/// ("Soft-Delete/Deactivate-Only Pattern": "hard delete retained only for Room/Building") and by
/// this build's own brief - see <see cref="DeleteAsync"/>'s remarks for why a `DELETE` endpoint
/// exists here despite tickets.md's Flagged Gaps reading as a blanket "no delete verb anywhere."
/// </summary>
public sealed class BuildingService(
    IBuildingRepository buildings,
    ICampusRepository campuses,
    IRoomRepository rooms,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static BuildingDto ToDto(Building building) => new(building.Id.Value, building.CampusId.Value, building.Name, building.Code, building.CreatedAt);

    public async Task<Result<BuildingDto>> CreateAsync(CreateBuildingRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("building.name_required", "Building name is required.");
        }

        var campus = await campuses.GetByIdAsync(new CampusId(request.CampusId), cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Error.NotFound("campus.not_found", $"No Campus exists with id '{request.CampusId}'.");
        }

        var now = clock.UtcNow;
        var building = Building.Create(campus.Id, request.Name, request.Code, now);
        buildings.Add(building);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Building", building.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { name = building.Name, campusId = building.CampusId.Value }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(building);
    }

    public async Task<Result<BuildingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var building = await buildings.GetByIdAsync(new BuildingId(id), cancellationToken).ConfigureAwait(false);
        return building is null
            ? Error.NotFound("building.not_found", $"No Building exists with id '{id}'.")
            : ToDto(building);
    }

    public async Task<BuildingListPage> ListAsync(Guid? campusId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var campusIdValue = campusId.HasValue ? new CampusId(campusId.Value) : (CampusId?)null;
        var items = await buildings.ListAsync(campusIdValue, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await buildings.CountAsync(campusIdValue, cancellationToken).ConfigureAwait(false);
        return new BuildingListPage(items.Select(ToDto).ToList(), total, skip, take);
    }

    /// <summary>
    /// design-decisions.md, "Soft-Delete/Deactivate-Only Pattern" + this build's own brief: hard
    /// delete is permitted for Building, gated on zero Rooms beneath it - a purely local check
    /// (Room is Organization's own table, unlike the cross-module reference check
    /// <see cref="RoomService.DeleteAsync"/> needs), row-locked across the check-plus-delete the
    /// same way a parent-active-status check is (design-decisions.md, "Parent-Active-Status
    /// Validation Mechanism") to close the same class of TOCTOU race against a concurrent
    /// `POST /rooms` targeting this Building.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var building = await buildings.GetByIdForUpdateAsync(new BuildingId(id), cancellationToken).ConfigureAwait(false);
        if (building is null)
        {
            return Result.Failure(Error.NotFound("building.not_found", $"No Building exists with id '{id}'."));
        }

        var hasRooms = await rooms.HasAnyUnderAsync(building.Id, cancellationToken).ConfigureAwait(false);
        if (hasRooms)
        {
            return Result.Failure(Error.Conflict("building.has_rooms", "Cannot hard-delete a Building that still has Room records - delete or reassign its Rooms first."));
        }

        buildings.Remove(building);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Building", id.ToString(), AuditActions.Delete, JsonSerializer.Serialize(new { name = building.Name }), null);
        return await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
    }
}
