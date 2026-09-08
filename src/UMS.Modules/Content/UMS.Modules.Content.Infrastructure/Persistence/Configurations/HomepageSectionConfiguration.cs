using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Content.Domain.HomepageSections;

namespace UMS.Modules.Content.Infrastructure.Persistence.Configurations;

/// <summary>CNT-10: `homepage_sections`.</summary>
internal sealed class HomepageSectionConfiguration : IEntityTypeConfiguration<HomepageSection>
{
    public void Configure(EntityTypeBuilder<HomepageSection> builder)
    {
        builder.ToTable("homepage_sections");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new HomepageSectionId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(s => s.SectionKey).HasColumnName("section_key").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Title).HasColumnName("title").IsRequired();
        builder.Property(s => s.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(s => s.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(s => s.ReferenceOrganizationNodeId).HasColumnName("reference_organization_node_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(s => s.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(s => s.SectionKey).IsUnique().HasDatabaseName("ux_homepage_sections_section_key");

        // design-decisions.md "Deterministic Ordering Tiebreaker": the SAME rule as Banner.
        builder.HasIndex(s => new { s.SortOrder, s.CreatedAt }).HasDatabaseName("ix_homepage_sections_sort_order_created_at");
    }
}
