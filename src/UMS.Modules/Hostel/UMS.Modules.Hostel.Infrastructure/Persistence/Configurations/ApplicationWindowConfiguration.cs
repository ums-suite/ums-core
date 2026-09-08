using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.ApplicationWindows;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationWindowConfiguration : IEntityTypeConfiguration<ApplicationWindow>
{
    public void Configure(EntityTypeBuilder<ApplicationWindow> builder)
    {
        builder.ToTable("application_windows");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasConversion(id => id.Value, value => new ApplicationWindowId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(w => w.SessionLabel).HasColumnName("session_label").HasMaxLength(200).IsRequired();
        builder.Property(w => w.OpensAt).HasColumnName("opens_at").IsRequired();
        builder.Property(w => w.ClosesAt).HasColumnName("closes_at").IsRequired();
        builder.Property(w => w.RulesVersion).HasColumnName("rules_version").IsRequired();
        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.Property(w => w.EligibleProgramIds)
            .HasConversion(JsonListConverters.GuidList)
            .Metadata.SetValueComparer(JsonListConverters.GuidListComparer);
        builder.Property(w => w.EligibleProgramIds).HasColumnName("eligible_program_ids").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();

        builder.Property(w => w.EligibleYears)
            .HasConversion(JsonListConverters.IntList)
            .Metadata.SetValueComparer(JsonListConverters.IntListComparer);
        builder.Property(w => w.EligibleYears).HasColumnName("eligible_years").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();

        builder.Property(w => w.EligibilityRules)
            .HasConversion(JsonListConverters.EligibilityRuleList)
            .Metadata.SetValueComparer(JsonListConverters.EligibilityRuleListComparer);
        builder.Property(w => w.EligibilityRules).HasColumnName("eligibility_rules").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
    }
}
