using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SessionId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasColumnName("user_id")
            .IsRequired();
        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_sessions_user_id");

        builder.Property(s => s.CurrentRefreshTokenHash).HasColumnName("current_refresh_token_hash").IsRequired();
        builder.Property(s => s.CurrentRefreshTokenExpiresAt).HasColumnName("current_refresh_token_expires_at").IsRequired();
        builder.Property(s => s.PreviousRefreshTokenHash).HasColumnName("previous_refresh_token_hash");
        builder.Property(s => s.PreviousRefreshTokenConsumedAt).HasColumnName("previous_refresh_token_consumed_at");
        builder.Property(s => s.UserAgent).HasColumnName("user_agent");
        builder.Property(s => s.CreatedFromIp).HasColumnName("created_from_ip");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.LastUsedAt).HasColumnName("last_used_at").IsRequired();
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");
        builder.Property(s => s.RevokedReason).HasColumnName("revoked_reason");
    }
}
