using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class MentorshipMatchConfiguration : IEntityTypeConfiguration<MentorshipMatch>
{
    public void Configure(EntityTypeBuilder<MentorshipMatch> builder)
    {
        builder.ToTable("mentorship_matches");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasConversion(id => id.Value, value => new MentorshipMatchId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(m => m.MentorAlumnusId).HasColumnName("mentor_alumnus_id").IsRequired();
        builder.Property(m => m.MenteeStudentId).HasColumnName("mentee_student_id").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.ProposedAt).HasColumnName("proposed_at").IsRequired();
        builder.Property(m => m.MentorAcceptedAt).HasColumnName("mentor_accepted_at");
        builder.Property(m => m.MenteeAcceptedAt).HasColumnName("mentee_accepted_at");
        builder.Property(m => m.ActivatedAt).HasColumnName("activated_at");
        builder.Property(m => m.EndedAt).HasColumnName("ended_at");
        builder.Property(m => m.EndedReason).HasColumnName("ended_reason").HasMaxLength(500);
        builder.Property(m => m.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(m => m.MentorAlumnusId).HasDatabaseName("ix_mentorship_matches_mentor_alumnus_id");
        builder.HasIndex(m => m.MenteeStudentId).HasDatabaseName("ix_mentorship_matches_mentee_student_id");
        builder.HasIndex(m => m.Status).HasDatabaseName("ix_mentorship_matches_status");
    }
}
