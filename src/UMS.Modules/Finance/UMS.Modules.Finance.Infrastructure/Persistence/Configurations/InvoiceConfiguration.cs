using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Finance.Domain.Invoices;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion(id => id.Value, value => new InvoiceId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(i => i.SourceModule).HasColumnName("source_module").HasMaxLength(100).IsRequired();
        builder.Property(i => i.SourceReferenceId).HasColumnName("source_reference_id").HasMaxLength(200).IsRequired();
        builder.Property(i => i.FeeType).HasColumnName("fee_type").HasMaxLength(100).IsRequired();
        builder.Property(i => i.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.PaidAt).HasColumnName("paid_at");
        builder.Property(i => i.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.ComplexProperty(i => i.TotalAmount, amount =>
        {
            amount.Property(a => a.Amount).HasColumnName("total_amount").HasPrecision(12, 2).IsRequired();
            amount.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        // requirement-spec.md §2: "Finance enforces a uniqueness constraint on (sourceModule,
        // sourceReferenceId, feeType) so a caller's own retry can never silently double-bill".
        builder.HasIndex(i => new { i.SourceModule, i.SourceReferenceId, i.FeeType })
            .IsUnique()
            .HasDatabaseName("ux_invoices_natural_key");

        builder.HasIndex(i => i.OwnerId).HasDatabaseName("ix_invoices_owner_id");

        builder.OwnsMany(i => i.Items, ConfigureItem);
        builder.Navigation(i => i.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // A separate, explicitly-typed method - not an inline lambda - for the OwnsMany build action:
    // an inline lambda whose OWN body also called a second overloaded generic builder method left
    // the compiler unable to infer OwnsMany's TRelatedEntity type argument correctly. Giving the
    // build action an explicit, fully-typed parameter here removes that ambiguity entirely.
    private static void ConfigureItem(OwnedNavigationBuilder<Invoice, InvoiceItem> item)
    {
        item.ToTable("invoice_items");
        item.WithOwner().HasForeignKey("InvoiceId");
        item.Property<InvoiceId>("InvoiceId").HasConversion(id => id.Value, value => new InvoiceId(value)).HasColumnName("invoice_id");
        item.Property<int>("Ordinal").HasColumnName("ordinal");
        item.HasKey("InvoiceId", "Ordinal");

        item.Property(x => x.FeeStructureId).HasColumnName("fee_structure_id").IsRequired();
        item.Property(x => x.FeeStructureVersion).HasColumnName("fee_structure_version").IsRequired();
        item.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();

        // Plain scalars, not a nested Money mapping - see InvoiceItem's own remarks: EF Core's
        // ComplexProperty builder is unavailable one level inside an already-owned collection in
        // this version, and OwnsOne/OwnsMany require a reference type (Money is a struct).
        item.Property(x => x.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        item.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        item.Ignore(x => x.AsMoney);
    }
}
