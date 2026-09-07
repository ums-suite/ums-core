using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.Applications;
using ApplicationId = UMS.Modules.Admission.Domain.Applications.ApplicationId;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Domain.Applications.Application>
{
    public void Configure(EntityTypeBuilder<Domain.Applications.Application> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new ApplicationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.ApplicantId).HasColumnName("applicant_id").IsRequired();
        builder.Property(a => a.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.ApplicationNumber).HasColumnName("application_number").HasMaxLength(50);
        builder.Property(a => a.ApplicationFeeInvoiceId).HasColumnName("application_fee_invoice_id");
        builder.Property(a => a.IsApplicationFeePaid).HasColumnName("is_application_fee_paid").IsRequired();
        builder.Property(a => a.ConfirmationFeeInvoiceId).HasColumnName("confirmation_fee_invoice_id");
        builder.Property(a => a.IsConfirmationFeePaid).HasColumnName("is_confirmation_fee_paid").IsRequired();
        builder.Property(a => a.AssignedTestSlotId).HasColumnName("assigned_test_slot_id");
        builder.Property(a => a.RollNumber).HasColumnName("roll_number").HasMaxLength(50);
        builder.Property(a => a.AdmitCardDocumentId).HasColumnName("admit_card_document_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(a => a.LockedAt).HasColumnName("locked_at");
        builder.Property(a => a.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // requirement-spec.md §4: "one Application per Applicant per AdmissionCampaign".
        builder.HasIndex(a => new { a.ApplicantId, a.CampaignId }).IsUnique().HasDatabaseName("ux_applications_applicant_campaign");
        builder.HasIndex(a => a.ApplicationNumber).IsUnique().HasDatabaseName("ux_applications_application_number").HasFilter("application_number IS NOT NULL");
        builder.HasIndex(a => a.ApplicationFeeInvoiceId).HasDatabaseName("ix_applications_application_fee_invoice_id");
        builder.HasIndex(a => a.ConfirmationFeeInvoiceId).HasDatabaseName("ix_applications_confirmation_fee_invoice_id");
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_applications_status");

        builder.OwnsMany(a => a.ProgramChoices, choice =>
        {
            choice.ToTable("application_program_choices");
            choice.UsePropertyAccessMode(PropertyAccessMode.Field);
            choice.WithOwner().HasForeignKey("ApplicationId");
            choice.Property<ApplicationId>("ApplicationId").HasConversion(id => id.Value, value => new ApplicationId(value)).HasColumnName("application_id");
            choice.Property<int>("Ordinal").HasColumnName("ordinal");
            choice.HasKey("ApplicationId", "Ordinal");

            choice.Property(c => c.ProgramId).HasColumnName("program_id").IsRequired();
            choice.Property(c => c.Rank).HasColumnName("rank").IsRequired();
        });
        builder.Navigation(a => a.ProgramChoices).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(a => a.Documents, document =>
        {
            document.ToTable("application_documents");
            document.UsePropertyAccessMode(PropertyAccessMode.Field);
            document.WithOwner().HasForeignKey("ApplicationId");
            document.Property<ApplicationId>("ApplicationId").HasConversion(id => id.Value, value => new ApplicationId(value)).HasColumnName("application_id");
            document.HasKey(d => d.Id);

            document.Property(d => d.Id).ValueGeneratedNever().HasColumnName("id");
            document.Property(d => d.DocumentType).HasColumnName("document_type").HasMaxLength(100).IsRequired();
            document.Property(d => d.FileReference).HasColumnName("file_reference").HasMaxLength(1000).IsRequired();
            document.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
            document.Property(d => d.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(1000);
            document.Property(d => d.UploadedAt).HasColumnName("uploaded_at").IsRequired();
            document.Property(d => d.ReviewedAt).HasColumnName("reviewed_at");
            document.Property(d => d.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        });
        builder.Navigation(a => a.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
