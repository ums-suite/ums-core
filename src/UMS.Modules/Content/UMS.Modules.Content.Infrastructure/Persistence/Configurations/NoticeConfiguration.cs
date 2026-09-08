using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Content.Domain.Notices;

namespace UMS.Modules.Content.Infrastructure.Persistence.Configurations;

/// <summary>CNT-1/2/3: `notices` + `notice_translations` (ADR-0011).</summary>
internal sealed class NoticeConfiguration : IEntityTypeConfiguration<Notice>
{
    public void Configure(EntityTypeBuilder<Notice> builder)
    {
        builder.ToTable("notices");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasConversion(id => id.Value, value => new NoticeId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(n => n.Title).HasColumnName("title").IsRequired();
        builder.Property(n => n.Body).HasColumnName("body").IsRequired();
        builder.Property(n => n.Audience).HasColumnName("audience").HasConversion<int>().IsRequired();
        builder.Property(n => n.OrganizationNodeId).HasColumnName("organization_node_id");
        builder.Property(n => n.IsUrgent).HasColumnName("is_urgent").IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.PublishAt).HasColumnName("publish_at");
        builder.Property(n => n.ExpireAt).HasColumnName("expire_at");
        builder.Property(n => n.PublishedAt).HasColumnName("published_at");
        builder.Property(n => n.ArchivedAt).HasColumnName("archived_at");
        builder.Property(n => n.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(n => n.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(n => n.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        // CNT-4: the job's own idempotent scan predicates (design-decisions.md "Scheduled-Publish
        // Job Exactly-Once Execution Mechanism").
        builder.HasIndex(n => new { n.Status, n.PublishAt }).HasDatabaseName("ix_notices_status_publish_at");
        builder.HasIndex(n => new { n.Status, n.ExpireAt }).HasDatabaseName("ix_notices_status_expire_at");
        builder.HasIndex(n => new { n.Audience, n.Status }).HasDatabaseName("ix_notices_audience_status");

        // ums-core-gotchas: NoticeTranslation rows added to an already-persisted Notice (e.g. a
        // Bengali translation supplied after initial English-only creation) hit EF Core's
        // spurious-UPDATE-instead-of-INSERT bug when keyed purely on a caller-supplied business
        // value - keyed on a shadow `Ordinal` column instead (mirrors Admission's
        // `EligibilityRule`/`SeatQuota` pattern exactly), with the real natural-key uniqueness
        // enforced by a separate unique index.
        builder.OwnsMany(n => n.Translations, translation =>
        {
            translation.ToTable("notice_translations");
            translation.UsePropertyAccessMode(PropertyAccessMode.Field);
            translation.WithOwner().HasForeignKey("NoticeId");
            translation.Property<NoticeId>("NoticeId").HasConversion(id => id.Value, value => new NoticeId(value)).HasColumnName("notice_id");
            translation.Property<int>("Ordinal").HasColumnName("ordinal");
            translation.HasKey("NoticeId", "Ordinal");
            translation.HasIndex("NoticeId", "LanguageCode").IsUnique().HasDatabaseName("ux_notice_translations_notice_language");

            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Title).HasColumnName("title").IsRequired();
            translation.Property(t => t.Body).HasColumnName("body").IsRequired();
        });
        builder.Navigation(n => n.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
