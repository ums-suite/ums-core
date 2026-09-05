using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.AcademicSessions;
using UMS.Shared.Domain;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class AcademicSessionConfiguration : IEntityTypeConfiguration<AcademicSession>
{
    public void Configure(EntityTypeBuilder<AcademicSession> builder)
    {
        builder.ToTable("academic_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, value => new AcademicSessionId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsOne(s => s.Code, code =>
        {
            code.Property(v => v.Value).HasColumnName("code").IsRequired();
            code.HasIndex(v => v.Value).IsUnique().HasDatabaseName("ux_academic_sessions_code");
        });
        builder.Navigation(s => s.Code).IsRequired();

        builder.OwnsMany(s => s.Semesters, semester =>
        {
            semester.ToTable("semesters");
            semester.Property(sm => sm.Id).HasConversion(id => id.Value, value => new SemesterId(value)).ValueGeneratedNever().HasColumnName("id");
            semester.Property<AcademicSessionId>("AcademicSessionId").HasConversion(id => id.Value, value => new AcademicSessionId(value)).HasColumnName("academic_session_id");
            semester.WithOwner().HasForeignKey("AcademicSessionId");
            semester.HasKey(sm => sm.Id);

            semester.Property(sm => sm.Name).HasColumnName("name").IsRequired();

            semester.OwnsOne(sm => sm.RegistrationWindow, window =>
            {
                window.Property(w => w.Start).HasColumnName("registration_start").IsRequired();
                window.Property(w => w.End).HasColumnName("registration_end").IsRequired();
            });
            semester.Navigation(sm => sm.RegistrationWindow).IsRequired();

            semester.OwnsOne(sm => sm.DropWindow, window =>
            {
                window.Property(w => w.Start).HasColumnName("drop_start").IsRequired();
                window.Property(w => w.End).HasColumnName("drop_end").IsRequired();
            });
            semester.Navigation(sm => sm.DropWindow).IsRequired();

            semester.HasIndex("AcademicSessionId").HasDatabaseName("ix_semesters_academic_session_id");
        });
        builder.Navigation(s => s.Semesters).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
