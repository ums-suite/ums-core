using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.Applicants;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        builder.ToTable("applicants");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new ApplicantId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.IdentityUserId).HasColumnName("identity_user_id").IsRequired();
        builder.Property(a => a.GivenName).HasColumnName("given_name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.FamilyName).HasColumnName("family_name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(a => a.Mobile).HasColumnName("mobile").HasMaxLength(32);
        builder.Property(a => a.DateOfBirth).HasColumnName("date_of_birth").IsRequired();
        builder.Property(a => a.IsEmailVerified).HasColumnName("is_email_verified").IsRequired();
        builder.Property(a => a.IsMobileVerified).HasColumnName("is_mobile_verified").IsRequired();
        builder.Property(a => a.PendingOtpHash).HasColumnName("pending_otp_hash").HasMaxLength(128);
        builder.Property(a => a.PendingOtpChannel).HasColumnName("pending_otp_channel").HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.PendingOtpExpiresAt).HasColumnName("pending_otp_expires_at");
        builder.Property(a => a.PendingOtpAttempts).HasColumnName("pending_otp_attempts").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(a => a.IdentityUserId).IsUnique().HasDatabaseName("ux_applicants_identity_user_id");

        builder.OwnsMany(a => a.AcademicHistory, record =>
        {
            record.ToTable("applicant_academic_records");
            record.UsePropertyAccessMode(PropertyAccessMode.Field);
            record.WithOwner().HasForeignKey("ApplicantId");
            record.Property<ApplicantId>("ApplicantId").HasConversion(id => id.Value, value => new ApplicantId(value)).HasColumnName("applicant_id");
            record.Property<int>("Ordinal").HasColumnName("ordinal");
            record.HasKey("ApplicantId", "Ordinal");

            record.Property(r => r.Board).HasColumnName("board").HasMaxLength(200).IsRequired();
            record.Property(r => r.ExamName).HasColumnName("exam_name").HasMaxLength(200).IsRequired();
            record.Property(r => r.PassingYear).HasColumnName("passing_year").IsRequired();
            record.OwnsOne(r => r.Score, score =>
            {
                score.UsePropertyAccessMode(PropertyAccessMode.Field);
                score.Property(s => s.Value).HasColumnName("score").HasPrecision(6, 2).IsRequired();
                score.Property(s => s.Scale).HasColumnName("score_scale").HasConversion<string>().HasMaxLength(20).IsRequired();
            });
        });
        builder.Navigation(a => a.AcademicHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
