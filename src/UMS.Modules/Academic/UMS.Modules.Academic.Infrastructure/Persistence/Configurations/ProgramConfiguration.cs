using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.Programs;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.ToTable("programs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new ProgramId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(p => p.DepartmentId).HasColumnName("department_id").IsRequired();
        builder.HasIndex(p => p.DepartmentId).HasDatabaseName("ix_programs_department_id");

        builder.Property(p => p.Code).HasColumnName("code").IsRequired();
        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ux_programs_code");

        builder.Property(p => p.Name).HasColumnName("name").IsRequired();
        builder.Property(p => p.MaxCreditsPerSemester).HasColumnName("max_credits_per_semester").IsRequired();
        builder.Property(p => p.RequiresAdvisorApproval).HasColumnName("requires_advisor_approval").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(p => p.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
    }
}
