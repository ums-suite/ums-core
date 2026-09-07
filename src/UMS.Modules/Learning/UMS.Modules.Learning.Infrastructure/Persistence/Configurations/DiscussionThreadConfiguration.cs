using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Learning.Domain.Discussions;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Configurations;

internal sealed class DiscussionThreadConfiguration : IEntityTypeConfiguration<DiscussionThread>
{
    public void Configure(EntityTypeBuilder<DiscussionThread> builder)
    {
        builder.ToTable("discussion_threads");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, value => new DiscussionThreadId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(t => t.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(t => t.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(t => t.CourseOfferingId).HasDatabaseName("ix_discussion_threads_course_offering_id");

        // requirement-spec.md §4: moderation is a state transition, never a delete - `body` and
        // `author_user_id` are retained on a Removed post, and there is no delete path anywhere in
        // this module that would remove the row.
        builder.OwnsMany(t => t.Posts, post =>
        {
            post.ToTable("discussion_posts");
            post.Property(p => p.Id).HasConversion(id => id.Value, value => new DiscussionPostId(value)).ValueGeneratedNever().HasColumnName("id");
            post.WithOwner().HasForeignKey(p => p.DiscussionThreadId);
            post.Property(p => p.DiscussionThreadId).HasConversion(id => id.Value, value => new DiscussionThreadId(value)).HasColumnName("discussion_thread_id");
            post.HasKey(p => p.Id);

            post.Property(p => p.ParentPostId)
                .HasConversion(id => id!.Value.Value, value => new DiscussionPostId(value))
                .HasColumnName("parent_post_id");
            post.Property(p => p.AuthorUserId).HasColumnName("author_user_id").IsRequired();
            post.Property(p => p.Body).HasColumnName("body").IsRequired();
            post.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            post.Property(p => p.ModerationStatus).HasColumnName("moderation_status").HasConversion<string>().HasMaxLength(32).IsRequired();
            post.Property(p => p.RemovedByUserId).HasColumnName("removed_by_user_id");
            post.Property(p => p.RemovedReason).HasColumnName("removed_reason").HasMaxLength(2000);
            post.Property(p => p.RemovedAt).HasColumnName("removed_at");
            post.Property(p => p.RestoredByUserId).HasColumnName("restored_by_user_id");
            post.Property(p => p.RestoredAt).HasColumnName("restored_at");
        });
        builder.Navigation(t => t.Posts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
