using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>LIB-10: the (loan_id, notice_date) idempotency backstop - see <see cref="OverdueNotice"/>'s own remarks.</summary>
internal sealed class OverdueNoticeConfiguration : IEntityTypeConfiguration<OverdueNotice>
{
    public void Configure(EntityTypeBuilder<OverdueNotice> builder)
    {
        builder.ToTable("overdue_notices");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(n => n.LoanId).HasColumnName("loan_id").IsRequired();
        builder.Property(n => n.NoticeDate).HasColumnName("notice_date").IsRequired();
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(n => new { n.LoanId, n.NoticeDate }).IsUnique().HasDatabaseName("ux_overdue_notices_loan_date");
    }
}
