using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Faculty.Domain.LeaveRequests;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Configurations;

internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LeaveRequestId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(l => l.FacultyMemberId).HasColumnName("faculty_member_id").IsRequired();
        builder.Property(l => l.RequesterUserId).HasColumnName("requester_user_id").IsRequired();
        builder.Property(l => l.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(l => l.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(l => l.Reason).HasColumnName("reason").IsRequired();
        builder.Property(l => l.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(l => l.RoutedDirectlyToAuthority).HasColumnName("routed_directly_to_authority").IsRequired();
        builder.Property(l => l.SupportingDocumentReference).HasColumnName("supporting_document_reference");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(l => l.DecidedAt).HasColumnName("decided_at");

        builder.Property(l => l.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(l => l.FacultyMemberId).HasDatabaseName("ix_leave_requests_faculty_member_id");

        builder.OwnsMany(l => l.ReasonTranslations, translation =>
        {
            translation.ToTable("leave_request_reason_translations");
            translation.WithOwner().HasForeignKey("LeaveRequestId");
            translation.HasKey("LeaveRequestId", "LanguageCode");
            translation.Property<LeaveRequestId>("LeaveRequestId")
                .HasConversion(id => id.Value, value => new LeaveRequestId(value))
                .HasColumnName("leave_request_id");
            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Text).HasColumnName("text").IsRequired();
        });
        builder.Navigation(l => l.ReasonTranslations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
