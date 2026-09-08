namespace UMS.Modules.Library.Domain.Catalog;

/// <summary>requirement-spec.md §2: "each independently tracked (accession/barcode number, condition, status: Available / OnLoan / Reserved / Lost / Withdrawn)."</summary>
public enum BookCopyStatus
{
    Available,
    Reserved,
    OnLoan,
    Lost,
    Withdrawn,
}
