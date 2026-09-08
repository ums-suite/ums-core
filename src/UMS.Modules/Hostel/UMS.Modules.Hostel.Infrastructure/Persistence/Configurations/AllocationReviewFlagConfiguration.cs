using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

/// <summary>HOS-17: design-decisions.md "Student-Status-Change Review Flag - Additive Side-Table" - a plain, insert-only table, never touched by <c>AllocationConfiguration</c>'s own indexes/constraints.</summary>
internal sealed class AllocationReviewFlagConfiguration : IEntityTypeConfiguration<AllocationReviewFlag>
{
    public void Configure(EntityTypeBuilder<AllocationReviewFlag> builder)
    {
        builder.ToTable("allocation_review_flags");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(f => f.AllocationId).HasColumnName("allocation_id").IsRequired();
        builder.Property(f => f.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(f => f.SourceEventReference).HasColumnName("source_event_reference").HasMaxLength(200).IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(f => f.AllocationId).HasDatabaseName("ix_allocation_review_flags_allocation_id");
    }
}
