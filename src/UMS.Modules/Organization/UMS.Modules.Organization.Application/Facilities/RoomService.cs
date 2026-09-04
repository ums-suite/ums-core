using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Facilities;

/// <summary>ORG-7: Room CRUD (requirement-spec.md organization §2/§3/§6/§8) - see <see cref="BuildingService"/>'s own remarks on why a hard-delete endpoint exists here.</summary>
public sealed class RoomService(
    IRoomRepository rooms,
    IBuildingRepository buildings,
    IRoomReferenceChecker referenceChecker,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static RoomDto ToDto(Room room) => new(room.Id.Value, room.BuildingId.Value, room.Name, room.Capacity, room.RoomType, room.CreatedAt);

    /// <summary>Locks the parent Building row for the duration of the exists check plus this insert, serializing against a concurrent `DELETE /buildings/{id}` targeting the same Building (design-decisions.md, "Parent-Active-Status Validation Mechanism").</summary>
    public async Task<Result<RoomDto>> CreateAsync(CreateRoomRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("room.name_required", "Room name/number is required.");
        }

        if (request.Capacity is < 0)
        {
            return Error.Validation("room.invalid_capacity", "Room capacity cannot be negative.");
        }

        var building = await buildings.GetByIdForUpdateAsync(new BuildingId(request.BuildingId), cancellationToken).ConfigureAwait(false);
        if (building is null)
        {
            return Error.NotFound("building.not_found", $"No Building exists with id '{request.BuildingId}'.");
        }

        var now = clock.UtcNow;
        var room = Room.Create(building.Id, request.Name, request.Capacity, request.RoomType, now);
        rooms.Add(room);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Room", room.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { name = room.Name, buildingId = room.BuildingId.Value }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(room);
    }

    public async Task<Result<RoomDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await rooms.GetByIdAsync(new RoomId(id), cancellationToken).ConfigureAwait(false);
        return room is null
            ? Error.NotFound("room.not_found", $"No Room exists with id '{id}'.")
            : ToDto(room);
    }

    public async Task<RoomListPage> ListByBuildingAsync(Guid buildingId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var buildingIdValue = new BuildingId(buildingId);
        var items = await rooms.ListByBuildingAsync(buildingIdValue, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await rooms.CountByBuildingAsync(buildingIdValue, cancellationToken).ConfigureAwait(false);
        return new RoomListPage(items.Select(ToDto).ToList(), total, skip, take);
    }

    /// <summary>
    /// edge-cases.md "Hard-delete cross-module reference check races a new reference being
    /// created" / design-decisions.md "Cross-Module Hard-Delete Reference Check Timing": the
    /// cross-module reference check is re-run as the very last step before the delete, inside this
    /// same transaction - the narrowest window achievable without Organization taking a lock
    /// inside Hostel's/Academic's own schema, which ADR-0002 forbids. A genuine, narrow race
    /// remains (documented, not eliminated) where a reference lands in the instant between this
    /// check and the commit below.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var room = await rooms.GetByIdAsync(new RoomId(id), cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure(Error.NotFound("room.not_found", $"No Room exists with id '{id}'."));
        }

        var hasReferences = await referenceChecker.HasAnyReferencesAsync(id, cancellationToken).ConfigureAwait(false);
        if (hasReferences)
        {
            return Result.Failure(Error.Conflict("room.has_references", "Cannot hard-delete a Room that another module still references."));
        }

        rooms.Remove(room);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Re-check immediately before commit (design-decisions.md's own phrase) - the upfront
        // check above narrows the common case; this second check, as the last read before the
        // delete lands, narrows the remaining window as far as it can go without a cross-schema
        // lock.
        var stillNoReferences = !await referenceChecker.HasAnyReferencesAsync(id, cancellationToken).ConfigureAwait(false);
        if (!stillNoReferences)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.Conflict("room.has_references", "Cannot hard-delete a Room that another module started referencing while this request was in flight."));
        }

        var auditRequest = audit.ToRequest("Room", id.ToString(), AuditActions.Delete, JsonSerializer.Serialize(new { name = room.Name, buildingId = room.BuildingId.Value }), null);
        return await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
    }
}
