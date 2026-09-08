using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.FundingBodies;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Application.FundingBodies;

/// <summary>RES-1: Admin/Research-Office managed reference data - no lifecycle, no Audit requirement named for it in requirement-spec.md §4's audit list (that list names Grant/Publication/InstitutionalRepositoryEntry mutations specifically), so a plain CRUD commit is sufficient here.</summary>
public sealed class FundingBodyService(IFundingBodyRepository repository, IUnitOfWork unitOfWork, IClock clock)
{
    public static FundingBodyDto ToDto(FundingBody fundingBody) => new(
        fundingBody.Id.Value,
        fundingBody.Name,
        fundingBody.Country,
        fundingBody.Type.ToString(),
        fundingBody.Website,
        fundingBody.CreatedAt);

    public async Task<Result<FundingBodyDto>> CreateAsync(CreateFundingBodyRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<FundingBodyType>(request.Type, ignoreCase: true, out var type))
        {
            return Error.Validation("fundingbody.invalid_type", $"'{request.Type}' is not a recognized FundingBody type.");
        }

        var created = FundingBody.Create(request.Name, request.Country, type, request.Website, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        repository.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<FundingBodyDto>> UpdateAsync(Guid id, UpdateFundingBodyRequest request, CancellationToken cancellationToken = default)
    {
        var fundingBody = await repository.GetByIdAsync(new FundingBodyId(id), cancellationToken).ConfigureAwait(false);
        if (fundingBody is null)
        {
            return Error.NotFound("fundingbody.not_found", $"No FundingBody exists with id '{id}'.");
        }

        if (!Enum.TryParse<FundingBodyType>(request.Type, ignoreCase: true, out var type))
        {
            return Error.Validation("fundingbody.invalid_type", $"'{request.Type}' is not a recognized FundingBody type.");
        }

        var updated = fundingBody.Update(request.Name, request.Country, type, request.Website);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(fundingBody);
    }

    public async Task<Result<FundingBodyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fundingBody = await repository.GetByIdAsync(new FundingBodyId(id), cancellationToken).ConfigureAwait(false);
        return fundingBody is null
            ? Error.NotFound("fundingbody.not_found", $"No FundingBody exists with id '{id}'.")
            : ToDto(fundingBody);
    }

    public async Task<FundingBodyListPage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await repository.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return new FundingBodyListPage(items.Select(ToDto).ToList(), skip, take);
    }
}
