using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.ResultPublications;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>design-decisions.md "Grade-Lock State Machine Design": <see cref="ResultPublication.Status"/> and its transition metadata columns are mutated ONLY via <c>IResultPublicationRepository.TryTransitionAsync</c>/<c>TryRejectAsync</c> (a state-guarded conditional <c>ExecuteUpdateAsync</c>) once the row exists - never through this entity's own tracked SaveChanges path.</summary>
internal sealed class ResultPublicationConfiguration : IEntityTypeConfiguration<ResultPublication>
{
    public void Configure(EntityTypeBuilder<ResultPublication> builder)
    {
        builder.ToTable("result_publications");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new ResultPublicationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.HasIndex(r => r.CourseOfferingId).IsUnique().HasDatabaseName("ux_result_publications_course_offering_id");

        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CalculatedAt).HasColumnName("calculated_at");
        builder.Property(r => r.RejectedAt).HasColumnName("rejected_at");
        builder.Property(r => r.RejectedByUserId).HasColumnName("rejected_by_user_id");
        builder.Property(r => r.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(r => r.LockedAt).HasColumnName("locked_at");
        builder.Property(r => r.LockedByUserId).HasColumnName("locked_by_user_id");
        builder.Property(r => r.ApprovedAt).HasColumnName("approved_at");
        builder.Property(r => r.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(r => r.PublishedAt).HasColumnName("published_at");
        builder.Property(r => r.PublishedByUserId).HasColumnName("published_by_user_id");
        builder.Property(r => r.ArchivedAt).HasColumnName("archived_at");
        builder.Property(r => r.CorrectionCount).HasColumnName("correction_count").IsRequired();

        builder.Property(r => r.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
    }
}
