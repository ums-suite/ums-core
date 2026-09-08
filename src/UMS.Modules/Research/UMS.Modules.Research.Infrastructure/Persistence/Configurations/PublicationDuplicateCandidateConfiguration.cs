using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

internal sealed class PublicationDuplicateCandidateConfiguration : IEntityTypeConfiguration<PublicationDuplicateCandidate>
{
    public void Configure(EntityTypeBuilder<PublicationDuplicateCandidate> builder)
    {
        builder.ToTable("publication_duplicate_candidates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new PublicationDuplicateCandidateId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.PublicationId).HasColumnName("publication_id").IsRequired();
        builder.Property(c => c.CandidatePublicationId).HasColumnName("candidate_publication_id").IsRequired();
        builder.Property(c => c.MatchReason).HasColumnName("match_reason").HasMaxLength(1000).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(c => c.Status).HasDatabaseName("ix_publication_duplicate_candidates_status");
        builder.HasIndex(c => c.PublicationId).HasDatabaseName("ix_publication_duplicate_candidates_publication_id");
    }
}
