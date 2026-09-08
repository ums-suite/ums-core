using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Research.Domain.Grants;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

internal sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.ToTable("grants");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasConversion(id => id.Value, value => new GrantId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(g => g.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(g => g.Description).HasColumnName("description").IsRequired();
        builder.Property(g => g.FundingBodyId).HasColumnName("funding_body_id").IsRequired();
        builder.Property(g => g.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(g => g.RequiresPiReassignment).HasColumnName("requires_pi_reassignment").IsRequired();
        builder.Property(g => g.IsPubliclyVisible).HasColumnName("is_publicly_visible").IsRequired();
        builder.Property(g => g.AwardDate).HasColumnName("award_date");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(g => g.ClosedAt).HasColumnName("closed_at");
        builder.Property(g => g.ReportedAt).HasColumnName("reported_at");
        builder.Property(g => g.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // design-decisions.md "Grant Funding-Amount Currency Handling": Money is a top-level field
        // on this aggregate root, so EF's ComplexProperty builder applies directly here (unlike
        // InvoiceItem's own nested-inside-an-OwnsMany case, which needs the flattened-scalar-columns
        // workaround - see that configuration's own remarks).
        builder.ComplexProperty(g => g.FundingAmount, amount =>
        {
            amount.Property(a => a.Amount).HasColumnName("funding_amount").HasPrecision(14, 2).IsRequired();
            amount.Property(a => a.Currency).HasColumnName("funding_currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(g => g.FundingPeriod, period =>
        {
            period.Property(p => p.Start).HasColumnName("funding_period_start").IsRequired();
            period.Property(p => p.End).HasColumnName("funding_period_end").IsRequired();
        });

        builder.HasIndex(g => g.FundingBodyId).HasDatabaseName("ix_grants_funding_body_id");
        builder.HasIndex(g => g.Status).HasDatabaseName("ix_grants_status");
        builder.HasIndex(g => g.IsPubliclyVisible).HasDatabaseName("ix_grants_is_publicly_visible");

        // ums-core-gotchas: a GrantInvestigator added to an already-persisted Grant (adding a
        // Co-Investigator later, or the PI-reassignment path's remove+add) hits EF Core's spurious
        // UPDATE-instead-of-INSERT bug when keyed purely on the caller-supplied FacultyMemberId -
        // keyed on a shadow `Ordinal` column instead (mirrors Content's NoticeTranslation/
        // Admission's EligibilityRule pattern exactly), with the real "one row per FacultyMember"
        // uniqueness enforced by a separate unique index.
        builder.OwnsMany(g => g.Investigators, investigator =>
        {
            investigator.ToTable("grant_investigators");
            investigator.UsePropertyAccessMode(PropertyAccessMode.Field);
            investigator.WithOwner().HasForeignKey("GrantId");
            investigator.Property<GrantId>("GrantId").HasConversion(id => id.Value, value => new GrantId(value)).HasColumnName("grant_id");
            investigator.Property<int>("Ordinal").HasColumnName("ordinal");
            investigator.HasKey("GrantId", "Ordinal");
            investigator.HasIndex("GrantId", "FacultyMemberId").IsUnique().HasDatabaseName("ux_grant_investigators_grant_faculty_member");

            investigator.Property(i => i.FacultyMemberId).HasColumnName("faculty_member_id").IsRequired();
            investigator.Property(i => i.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(30).IsRequired();
            investigator.Property(i => i.AddedAt).HasColumnName("added_at").IsRequired();
        });
        builder.Navigation(g => g.Investigators).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
