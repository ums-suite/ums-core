namespace UMS.Modules.Library.Domain.Catalog;

/// <summary>requirement-spec.md §9 decision 4: a licensed digital resource's concurrent-seat count is modeled as N <see cref="CopyType.Digital"/> <see cref="BookCopy"/> rows, reusing the identical scarcity machinery as <see cref="Physical"/> copies.</summary>
public enum CopyType
{
    Physical,
    Digital,
}
