using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Finance.Domain.FeeStructures;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Configurations;

internal sealed class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
    public void Configure(EntityTypeBuilder<FeeStructure> builder)
    {
        builder.ToTable("fee_structures");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasConversion(id => id.Value, value => new FeeStructureId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(f => f.FeeType).HasColumnName("fee_type").HasMaxLength(100).IsRequired();
        builder.Property(f => f.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(f => f.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(f => f.EffectiveTo).HasColumnName("effective_to");
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
        builder.Ignore(f => f.Applicability);

        // Plain scalars, not a nested value object - a unique index spanning FeeType together with
        // Applicability's own members needs all four as direct properties on THIS entity; a nested
        // ComplexProperty/OwnsOne cannot participate in the owner's own HasIndex in this EF Core
        // version (FeeStructure's own remarks). FeeStructure.Applicability (computed) reconstructs
        // the validated value object for any caller that wants it back.
        builder.Property(f => f.ApplicabilityType).HasColumnName("applicability_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(f => f.ApplicabilityReferenceId).HasColumnName("applicability_reference_id");
        builder.Property(f => f.ApplicabilityServiceName).HasColumnName("applicability_service_name").HasMaxLength(200);

        builder.ComplexProperty(f => f.Amount, amount =>
        {
            amount.Property(a => a.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
            amount.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        // requirement-spec.md §2: exactly one Active FeeStructure per (FeeType, Applicability) pair,
        // enforced at the database, not only by FeeStructureService's own read-then-check
        // (FeeStructureService.PublishNewVersionAsync deprecates the prior row in the SAME
        // transaction as inserting the next one, so this index is never transiently violated by a
        // correct caller - only by a genuine bug or race).
        builder.HasIndex(f => new { f.FeeType, f.ApplicabilityType, f.ApplicabilityReferenceId, f.ApplicabilityServiceName })
            .IsUnique()
            .HasDatabaseName("ux_fee_structures_active")
            .HasFilter("status = 'Active'");
    }
}
