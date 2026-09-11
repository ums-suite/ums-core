using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Career.Domain.Employers;

namespace UMS.Modules.Career.Infrastructure.Persistence.Configurations;

internal sealed class EmployerProfileConfiguration : IEntityTypeConfiguration<EmployerProfile>
{
    public void Configure(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.ToTable("employer_profiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => new EmployerProfileId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(e => e.CompanyName).HasColumnName("company_name").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Industry).HasColumnName("industry").HasMaxLength(200);
        builder.Property(e => e.Website).HasColumnName("website").HasMaxLength(500);
        builder.Property(e => e.ContactName).HasColumnName("contact_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContactEmail).HasColumnName("contact_email").HasMaxLength(300).IsRequired();
        builder.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
        builder.Property(e => e.VerificationNote).HasColumnName("verification_note").HasMaxLength(1000);
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(e => e.IsArchived).HasDatabaseName("ix_employer_profiles_is_archived");
    }
}
