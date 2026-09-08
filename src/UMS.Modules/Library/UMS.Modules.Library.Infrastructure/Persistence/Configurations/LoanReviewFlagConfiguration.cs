using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

internal sealed class LoanReviewFlagConfiguration : IEntityTypeConfiguration<LoanReviewFlag>
{
    public void Configure(EntityTypeBuilder<LoanReviewFlag> builder)
    {
        builder.ToTable("loan_review_flags");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(f => f.LoanId).HasColumnName("loan_id").IsRequired();
        builder.Property(f => f.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(f => f.SourceEventReference).HasColumnName("source_event_reference").HasMaxLength(200).IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(f => f.LoanId).HasDatabaseName("ix_loan_review_flags_loan_id");
    }
}
