using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Complaints;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.ToTable("complaints");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new ComplaintId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(c => c.AllocationId).HasColumnName("allocation_id").IsRequired();
        builder.Property(c => c.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(4000);
        builder.Property(c => c.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(c => c.StudentId).HasDatabaseName("ix_complaints_student_id");
        builder.HasIndex(c => new { c.StudentId, c.AllocationId, c.CreatedAt }).HasDatabaseName("ix_complaints_dedupe_window");

        // design-decisions.md "Complaint Deduplication Mechanism": the client-supplied idempotency
        // key is the primary mechanism - unique when present, best-effort only (no constraint) for
        // the server-side dedupe-window fallback, matching this NOT being a Money-and-Academic-
        // Standing-criticality invariant.
        builder.HasIndex(c => c.IdempotencyKey).IsUnique().HasDatabaseName("ux_complaints_idempotency_key").HasFilter("idempotency_key IS NOT NULL");
    }
}
