using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

internal sealed class InstitutionalRepositoryEntryConfiguration : IEntityTypeConfiguration<InstitutionalRepositoryEntry>
{
    public void Configure(EntityTypeBuilder<InstitutionalRepositoryEntry> builder)
    {
        builder.ToTable("institutional_repository_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => new InstitutionalRepositoryEntryId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.WorkType).HasColumnName("work_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.SupervisingFacultyMemberId).HasColumnName("supervising_faculty_member_id");
        builder.Property(e => e.DepositDate).HasColumnName("deposit_date").IsRequired();
        builder.Property(e => e.ArtifactId).HasColumnName("artifact_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsOne(e => e.Depositor, depositor =>
        {
            depositor.Property(d => d.Name).HasColumnName("depositor_name").HasMaxLength(300).IsRequired();
            depositor.Property(d => d.FacultyMemberId).HasColumnName("depositor_faculty_member_id");
        });

        builder.OwnsOne(e => e.Embargo, embargo =>
        {
            embargo.Property(x => x.IsEmbargoed).HasColumnName("is_embargoed").IsRequired();
            embargo.Property(x => x.EmbargoEndDate).HasColumnName("embargo_end_date");
            embargo.Property(x => x.AccessLevel).HasColumnName("access_level").HasConversion<string>().HasMaxLength(30).IsRequired();

            // design-decisions.md "InstitutionalRepositoryEntry Embargo-Lift Mechanism": the daily
            // worker's own lapsed-embargo sweep predicate (`isEmbargoed = true AND embargoEndDate <=
            // today`) and the public-showcase's own exclusion filter both hit this index.
            embargo.HasIndex(x => new { x.IsEmbargoed, x.EmbargoEndDate }).HasDatabaseName("ix_institutional_repository_entries_embargo");
        });

        builder.HasIndex(e => e.SupervisingFacultyMemberId).HasDatabaseName("ix_institutional_repository_entries_supervisor");
    }
}
