using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Chapters;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class AlumniChapterConfiguration : IEntityTypeConfiguration<AlumniChapter>
{
    public void Configure(EntityTypeBuilder<AlumniChapter> builder)
    {
        builder.ToTable("alumni_chapters");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new AlumniChapterId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(c => c.Region).HasColumnName("region").HasMaxLength(200);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // ums-core-gotchas: a ChapterMembership added to an already-persisted AlumniChapter (a Join
        // call post-creation) hits EF Core's spurious UPDATE-instead-of-INSERT bug when keyed purely
        // on the caller-supplied AlumnusId - keyed on a shadow `Ordinal` column instead, with the real
        // "one membership row per (Chapter, Alumnus)" uniqueness enforced by a separate unique index.
        builder.OwnsMany(c => c.Memberships, membership =>
        {
            membership.ToTable("chapter_memberships");
            membership.UsePropertyAccessMode(PropertyAccessMode.Field);
            membership.WithOwner().HasForeignKey("ChapterId");
            membership.Property<AlumniChapterId>("ChapterId").HasConversion(id => id.Value, value => new AlumniChapterId(value)).HasColumnName("chapter_id");
            membership.Property<int>("Ordinal").HasColumnName("ordinal");
            membership.HasKey("ChapterId", "Ordinal");
            membership.HasIndex("ChapterId", "AlumnusId").IsUnique().HasDatabaseName("ux_chapter_memberships_chapter_alumnus");

            membership.Property(m => m.AlumnusId).HasConversion(id => id.Value, value => new Domain.Alumni.AlumnusId(value)).HasColumnName("alumnus_id").IsRequired();
            membership.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();
        });
        builder.Navigation(c => c.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
