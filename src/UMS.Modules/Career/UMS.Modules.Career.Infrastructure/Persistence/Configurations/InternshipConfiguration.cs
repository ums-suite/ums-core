using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Infrastructure.Persistence.Configurations;

/// <summary>
/// `EligibilityCriteria` is stored as three plain scalar columns (`_eligibilityProgramIds` mapped to
/// a Postgres `uuid[]` array column) - never a `ComplexProperty` (ums-core-gotchas: a
/// `ComplexProperty`-mapped value object with a collection member does not compose cleanly here).
/// </summary>
internal sealed class InternshipConfiguration : IEntityTypeConfiguration<Internship>
{
    public void Configure(EntityTypeBuilder<Internship> builder)
    {
        builder.ToTable("internships");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion(id => id.Value, value => new InternshipId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(i => i.EmployerProfileId).HasColumnName("employer_profile_id").IsRequired();
        builder.Property(i => i.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(i => i.Description).HasColumnName("description").HasMaxLength(8000);
        builder.Property(i => i.Location).HasColumnName("location").HasMaxLength(300);
        builder.Property(i => i.StipendNote).HasColumnName("stipend_note").HasMaxLength(500);
        builder.Property(i => i.ApplicationDeadline).HasColumnName("application_deadline").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(i => i.WithdrawalReason).HasColumnName("withdrawal_reason").HasMaxLength(1000);
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.PublishedAt).HasColumnName("published_at");
        builder.Property(i => i.EligibilityMinCgpa).HasColumnName("eligibility_min_cgpa").HasColumnType("numeric(3,2)");
        builder.Property(i => i.EligibilityMinYearOfStudy).HasColumnName("eligibility_min_year_of_study");
        builder.Property(i => i.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.Property<List<Guid>>("_eligibilityProgramIds")
            .HasColumnName("eligibility_program_ids")
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.HasIndex(i => i.Status).HasDatabaseName("ix_internships_status");
        builder.HasIndex(i => i.EmployerProfileId).HasDatabaseName("ix_internships_employer_profile_id");
        builder.HasIndex(i => new { i.Status, i.ApplicationDeadline }).HasDatabaseName("ix_internships_status_deadline");
    }
}
