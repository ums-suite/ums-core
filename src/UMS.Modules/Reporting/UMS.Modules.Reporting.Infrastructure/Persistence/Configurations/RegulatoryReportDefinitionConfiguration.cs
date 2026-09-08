using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Configurations;

internal sealed class RegulatoryReportDefinitionConfiguration : IEntityTypeConfiguration<RegulatoryReportDefinition>
{
    public void Configure(EntityTypeBuilder<RegulatoryReportDefinition> builder)
    {
        builder.ToTable("regulatory_report_definitions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, value => new RegulatoryReportDefinitionId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(d => d.FiltersJson).HasColumnName("filters_json").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.SourceQueryReferencesJson).HasColumnName("source_query_references_json").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.SupportedFormats).HasColumnName("supported_formats").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(d => d.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(d => d.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(d => d.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(d => d.Name).IsUnique().HasDatabaseName("ux_regulatory_report_definitions_name");

        // EF Core 10.0.11 gotcha: RegulatoryReportDefinition.UpdateConfiguration() clears and
        // rebuilds this owned collection from scratch with FRESH ReportFieldSelection instances
        // that can share the same domain-visible Ordinal values (0, 1, 2, ...) as the rows they
        // replace. A business-value key (e.g. keying on Ordinal itself, as MeritListConfiguration's
        // own MeritListEntry does) misfires UPDATE instead of DELETE+INSERT against an
        // already-persisted parent in that scenario. A DB-generated shadow surrogate key ("RowId"),
        // fully independent of the domain-visible Ordinal column, sidesteps this: EF always sees a
        // brand-new row as having no prior identity to collide with.
        builder.OwnsMany(d => d.FieldSelections, field =>
        {
            field.ToTable("regulatory_report_definition_fields");
            field.WithOwner().HasForeignKey("DefinitionId");

            // The shadow FK must be declared as the OWNER's own strongly-typed id CLR type (with
            // its own matching conversion), not a plain Guid - EF Core rejects a Guid-typed FK
            // against a RegulatoryReportDefinitionId-typed principal key as "not compatible"
            // (mirrors Faculty's own ResearchProfileConfiguration/Publications shadow-FK shape).
            field.Property<RegulatoryReportDefinitionId>("DefinitionId")
                .HasConversion(id => id.Value, value => new RegulatoryReportDefinitionId(value))
                .HasColumnName("definition_id");

            field.Property<int>("RowId").ValueGeneratedOnAdd().HasColumnName("row_id");
            field.HasKey("RowId");

            field.Property(f => f.FieldKey).HasColumnName("field_key").HasMaxLength(200).IsRequired();
            field.Property(f => f.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
            field.Property(f => f.Ordinal).HasColumnName("ordinal").IsRequired();

            field.HasIndex("DefinitionId").HasDatabaseName("ix_regulatory_report_definition_fields_definition_id");
        });
        builder.Navigation(d => d.FieldSelections).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
