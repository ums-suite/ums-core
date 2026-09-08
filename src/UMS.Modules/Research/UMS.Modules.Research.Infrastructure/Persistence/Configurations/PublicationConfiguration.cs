using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

internal sealed class PublicationConfiguration : IEntityTypeConfiguration<Publication>
{
    public void Configure(EntityTypeBuilder<Publication> builder)
    {
        builder.ToTable("publications");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new PublicationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(1000).IsRequired();
        builder.Property(p => p.IsPubliclyVisible).HasColumnName("is_publicly_visible").IsRequired();
        builder.Property(p => p.MergedIntoPublicationId).HasColumnName("merged_into_publication_id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsOne(p => p.Venue, venue =>
        {
            venue.Property(v => v.Type).HasColumnName("venue_type").HasConversion<string>().HasMaxLength(20).IsRequired();
            venue.Property(v => v.Name).HasColumnName("venue_name").HasMaxLength(500).IsRequired();
            venue.Property(v => v.Publisher).HasColumnName("venue_publisher").HasMaxLength(300);
        });

        builder.OwnsOne(p => p.Citation, citation =>
        {
            citation.Property(c => c.Doi).HasColumnName("doi").HasMaxLength(255);
            citation.Ignore(c => c.NormalizedDoi);
            citation.Property(c => c.PublicationDate).HasColumnName("publication_date").IsRequired();
            citation.Property(c => c.CitationCount).HasColumnName("citation_count");
        });

        // requirement-spec.md §4: "A Publication's DOI, when present, is globally unique within
        // Research's schema (case-insensitive, normalized) - enforced by a database unique
        // constraint, not an application-level check alone." A Postgres STORED generated column
        // (computed from the "doi" column OwnsOne(Citation) mapped onto this same table above) plus
        // a partial unique index on it - design-decisions.md's duplicate-detection decision.
        builder.Property<string?>("NormalizedDoi")
            .HasColumnName("normalized_doi")
            .HasComputedColumnSql("lower(btrim(doi))", stored: true);
        builder.HasIndex("NormalizedDoi").IsUnique().HasFilter("normalized_doi IS NOT NULL").HasDatabaseName("ux_publications_normalized_doi");

        builder.HasIndex(p => p.IsPubliclyVisible).HasDatabaseName("ix_publications_is_publicly_visible");
        builder.HasIndex(p => p.MergedIntoPublicationId).HasDatabaseName("ix_publications_merged_into_publication_id");

        // ums-core-gotchas: an AuthorEntry appended post-creation (e.g. Update() replacing the whole
        // list) hits the same shadow-Ordinal-key requirement as GrantInvestigator above, even though
        // the domain-level `Order` property is separate and always present (§4's "order is never
        // inferred from array/list position" is about the DOMAIN-visible ordering; the EF-level key
        // still needs its own shadow Ordinal underneath).
        builder.OwnsMany(p => p.Authors, author =>
        {
            author.ToTable("publication_authors");
            author.UsePropertyAccessMode(PropertyAccessMode.Field);
            author.WithOwner().HasForeignKey("PublicationId");
            author.Property<PublicationId>("PublicationId").HasConversion(id => id.Value, value => new PublicationId(value)).HasColumnName("publication_id");
            author.Property<int>("Ordinal").HasColumnName("ordinal");
            author.HasKey("PublicationId", "Ordinal");

            author.Property(a => a.Order).HasColumnName("author_order").IsRequired();
            author.Property(a => a.FacultyMemberId).HasColumnName("faculty_member_id");
            author.Property(a => a.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
            author.Property(a => a.Affiliation).HasColumnName("affiliation").HasMaxLength(300);
            author.Property(a => a.IsCorrespondingAuthor).HasColumnName("is_corresponding_author").IsRequired();
        });
        builder.Navigation(p => p.Authors).UsePropertyAccessMode(PropertyAccessMode.Field);

        // requirement-spec.md §3: fundedByGrantIds references Grant by id only, never an object
        // graph load (§9) - a plain jsonb list of ids, not a relational join table, is sufficient
        // and keeps this explicit at the persistence layer too.
        builder.Property(p => p.FundedByGrantIds)
            .HasConversion(JsonGuidListConverter.Converter)
            .Metadata.SetValueComparer(JsonGuidListConverter.Comparer);
        builder.Property(p => p.FundedByGrantIds).HasColumnName("funded_by_grant_ids").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
    }
}
