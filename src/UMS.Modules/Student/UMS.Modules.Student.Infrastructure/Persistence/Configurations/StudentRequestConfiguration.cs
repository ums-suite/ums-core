using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Student.Domain.StudentRequests;

namespace UMS.Modules.Student.Infrastructure.Persistence.Configurations;

/// <summary>STU-9..STU-14. design-decisions.md "StudentRequest Dedup Mechanism": the partial unique index below is the REAL enforcement for the one-open-request-per-type invariant - the application-level pre-check (<c>StudentRequestService</c>) is only a fast-path UX nicety.</summary>
internal sealed class StudentRequestConfiguration : IEntityTypeConfiguration<StudentRequest>
{
    public void Configure(EntityTypeBuilder<StudentRequest> builder)
    {
        builder.ToTable("student_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new StudentRequestId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(r => r.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(r => r.RequestType).HasColumnName("request_type").HasConversion<string>().IsRequired();
        builder.Property(r => r.Details).HasColumnName("details").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(r => r.ReviewScopeNodeId).HasColumnName("review_scope_node_id");
        builder.Property(r => r.IsAgainstOwnDepartmentHead).HasColumnName("is_against_own_department_head").IsRequired();
        builder.Property(r => r.GeneratedDocumentId).HasColumnName("generated_document_id");
        builder.Property(r => r.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(r => r.DecisionReason).HasColumnName("decision_reason");
        builder.Property(r => r.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(r => r.DecidedAt).HasColumnName("decided_at");
        builder.Property(r => r.FulfilledAt).HasColumnName("fulfilled_at");

        builder.Property(r => r.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(r => r.StudentId).HasDatabaseName("ix_student_requests_student_id");
        builder.HasIndex(r => r.ReviewScopeNodeId).HasDatabaseName("ix_student_requests_review_scope_node_id");

        // design-decisions.md's actual enforcement mechanism (edge-cases.md "Two concurrent
        // StudentRequest submissions of the same type") - filtered to the two non-terminal
        // statuses; the column values are the enum-name strings HasConversion<string>() writes.
        builder.HasIndex(r => new { r.StudentId, r.RequestType })
            .IsUnique()
            .HasDatabaseName("ux_student_requests_student_id_request_type_open")
            .HasFilter("status IN ('Submitted', 'UnderReview')");
    }
}
