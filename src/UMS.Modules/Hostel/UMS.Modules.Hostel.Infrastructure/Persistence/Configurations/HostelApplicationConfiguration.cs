using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Applications;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class HostelApplicationConfiguration : IEntityTypeConfiguration<HostelApplication>
{
    public void Configure(EntityTypeBuilder<HostelApplication> builder)
    {
        builder.ToTable("hostel_applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new HostelApplicationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(a => a.ApplicationWindowId).HasColumnName("application_window_id").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.YearOfStudy).HasColumnName("year_of_study").IsRequired();
        builder.Property(a => a.HasFinancialNeed).HasColumnName("has_financial_need").IsRequired();
        builder.Property(a => a.HomeDistrictDistanceKm).HasColumnName("home_district_distance_km").HasPrecision(10, 2);
        builder.Property(a => a.EligibilityScore).HasColumnName("eligibility_score").HasPrecision(10, 2);
        builder.Property(a => a.IsEligible).HasColumnName("is_eligible");
        builder.Property(a => a.RankPosition).HasColumnName("rank_position");
        builder.Property(a => a.DecisionReason).HasColumnName("decision_reason").HasMaxLength(1000);
        builder.Property(a => a.AllocationId).HasColumnName("allocation_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(a => a.RankedAt).HasColumnName("ranked_at");
        builder.Property(a => a.DecidedAt).HasColumnName("decided_at");
        builder.Property(a => a.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(a => new { a.ApplicationWindowId, a.Status }).HasDatabaseName("ix_hostel_applications_window_status");

        // requirement-spec.md §9 Open Question: "whether a Student may hold simultaneous applications
        // across multiple hostels in the same session" is unconfirmed with the Hostel Office - the
        // current assumption (one open HostelApplication per Student per session/window) is enforced
        // here as a DB-level backstop behind HostelApplicationService's own pre-check, not resolved
        // as a final answer.
        builder.HasIndex(a => new { a.StudentId, a.ApplicationWindowId })
            .IsUnique()
            .HasDatabaseName("ux_hostel_applications_student_window_open")
            .HasFilter("status IN ('Draft', 'Submitted', 'Ranked', 'Waitlisted', 'Approved')");

        builder.OwnsMany(a => a.Preferences, preference =>
        {
            preference.ToTable("hostel_application_preferences");
            preference.UsePropertyAccessMode(PropertyAccessMode.Field);
            preference.WithOwner().HasForeignKey("HostelApplicationId");
            preference.Property<HostelApplicationId>("HostelApplicationId").HasConversion(id => id.Value, value => new HostelApplicationId(value)).HasColumnName("hostel_application_id");

            // Shadow Ordinal key, not (HostelApplicationId, HostelId) - the EF Core 10 owned-collection
            // insert-vs-update misfire this repo's own ProgramChoice/InvoiceItem precedent already
            // works around: preferences are replaced wholesale on an already-persisted, already-
            // tracked Application (ReplacePreferences), the exact shape that bug class hits.
            preference.Property<int>("Ordinal").HasColumnName("ordinal");
            preference.HasKey("HostelApplicationId", "Ordinal");

            preference.Property(p => p.HostelId).HasColumnName("hostel_id").IsRequired();
            preference.Property(p => p.PreferredRoomType).HasColumnName("preferred_room_type").HasConversion<string>().HasMaxLength(50).IsRequired();
            preference.Property(p => p.Rank).HasColumnName("rank").IsRequired();
        });
        builder.Navigation(a => a.Preferences).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
