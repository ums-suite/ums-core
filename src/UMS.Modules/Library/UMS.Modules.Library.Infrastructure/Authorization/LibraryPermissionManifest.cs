using UMS.Modules.Library.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Library.Infrastructure.Authorization;

internal sealed class LibraryPermissionManifest : IPermissionManifest
{
    public string OwningModule => "library";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(LibraryPermissions.CatalogManage, "Create and manage Book/BookCopy/Author/Category catalog entries (Librarian)."),
        new(LibraryPermissions.LoanIssue, "Issue a Loan against a specific BookCopy (Librarian/front-desk)."),
        new(LibraryPermissions.LoanManage, "Renew or return a Loan on a borrower's behalf (Librarian)."),
        new(LibraryPermissions.FineWaive, "Waive an outstanding Fine with an audited reason (Librarian)."),
        new(LibraryPermissions.CopyWriteOff, "Report a BookCopy lost and force-close its Loan (Librarian)."),
        new(LibraryPermissions.ReviewFlagView, "View LoanReviewFlag/BookReservationSupplyFlag advisory signals (Librarian)."),
    ];
}
