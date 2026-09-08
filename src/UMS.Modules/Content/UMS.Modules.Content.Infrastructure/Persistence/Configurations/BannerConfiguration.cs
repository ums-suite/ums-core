using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Content.Domain.Banners;

namespace UMS.Modules.Content.Infrastructure.Persistence.Configurations;

/// <summary>CNT-8/CNT-9: `banners` - identical scheduling shape as Notice.</summary>
internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("banners");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, value => new BannerId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(b => b.Headline).HasColumnName("headline").IsRequired();
        builder.Property(b => b.ImageUrl).HasColumnName("image_url").IsRequired();
        builder.Property(b => b.LinkUrl).HasColumnName("link_url");
        builder.Property(b => b.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.PublishAt).HasColumnName("publish_at");
        builder.Property(b => b.ExpireAt).HasColumnName("expire_at");
        builder.Property(b => b.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(b => b.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(b => new { b.Status, b.PublishAt }).HasDatabaseName("ix_banners_status_publish_at");
        builder.HasIndex(b => new { b.Status, b.ExpireAt }).HasDatabaseName("ix_banners_status_expire_at");

        // design-decisions.md "Deterministic Ordering Tiebreaker": sort_order asc, created_at asc.
        builder.HasIndex(b => new { b.SortOrder, b.CreatedAt }).HasDatabaseName("ix_banners_sort_order_created_at");
    }
}
