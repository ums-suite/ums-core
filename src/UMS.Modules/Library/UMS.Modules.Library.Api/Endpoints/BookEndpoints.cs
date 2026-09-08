using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Library.Application.Catalog;
using UMS.Modules.Library.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Api.Endpoints;

/// <summary>LIB-1/LIB-2/LIB-3/LIB-17: requirement-spec.md §6 catalog rows.</summary>
internal static class BookEndpoints
{
    public static void MapBookEndpoints(this RouteGroupBuilder group)
    {
        var books = group.MapGroup("/books");

        books.MapGet("/", async (string? q, Guid? categoryId, Guid? authorId, int? page, int? pageSize, BookSearchService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchAsync(q, categoryId, authorId, page ?? 1, pageSize ?? 20, cancellationToken).ConfigureAwait(false)))
            .RequireLiveSession();

        books.MapGet("/{id:guid}", async (Guid id, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.GetBookByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        books.MapGet("/{id:guid}/copies", async (Guid id, CatalogService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCopiesByBookAsync(id, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        books.MapPost("/", async (CreateBookRequest body, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBookAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.CatalogManage);

        books.MapPut("/{id:guid}", async (Guid id, UpdateBookRequest body, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateBookAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.CatalogManage);

        books.MapPost("/{id:guid}/withdraw", async (Guid id, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.WithdrawBookAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.CatalogManage);

        books.MapPost("/{id:guid}/copies", async (Guid id, CreateBookCopyRequestBody body, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBookCopyAsync(new CreateBookCopyRequest(id, body.AccessionNumber, body.Condition, body.CopyType), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.CatalogManage);

        var authors = group.MapGroup("/authors");
        authors.MapGet("/", async (string? q, CatalogService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchAuthorsAsync(q, cancellationToken).ConfigureAwait(false))).RequireLiveSession();
        authors.MapPost("/", async (CreateAuthorRequest body, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAuthorAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.CatalogManage);

        var categories = group.MapGroup("/categories");
        categories.MapGet("/", async (CatalogService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllCategoriesAsync(cancellationToken).ConfigureAwait(false))).RequireLiveSession();
        categories.MapPost("/", async (CreateCategoryRequest body, CatalogService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCategoryAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.CatalogManage);

        // LIB-17: not itself named in requirement-spec.md §6's own table, mirroring Hostel's own
        // "review-flags" precedent (an endpoint the spec's API table doesn't literally list, added
        // as a natural surface for the mechanism the tickets do require).
        var bookCopies = group.MapGroup("/book-copies");
        bookCopies.MapPost("/{id:guid}/report-lost", async (Guid id, HttpContext httpContext, LostCopyWriteOffService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReportLostAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(LibraryPermissions.CopyWriteOff);
    }
}

internal sealed record CreateBookCopyRequestBody(string AccessionNumber, string? Condition, string CopyType);
