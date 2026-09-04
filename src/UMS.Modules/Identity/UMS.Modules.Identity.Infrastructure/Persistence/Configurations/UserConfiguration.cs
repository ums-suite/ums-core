using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Username/email/mobile/university-id all map to Postgres <c>citext</c> columns - case-
/// insensitive equality/uniqueness natively, matching "resolved to exactly one User" (identity
/// §2) without hand-rolling <c>lower()</c> comparisons at every call site.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(u => u.Username).HasColumnName("username").HasColumnType("citext").IsRequired();
        builder.HasIndex(u => u.Username).IsUnique().HasDatabaseName("ux_users_username");

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasColumnName("email")
            .HasColumnType("citext")
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");

        builder.Property(u => u.Mobile)
            .HasConversion(
                mobile => mobile == null ? null : mobile.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasColumnName("mobile")
            .HasColumnType("citext");
        builder.HasIndex(u => u.Mobile).IsUnique().HasDatabaseName("ux_users_mobile").HasFilter("\"mobile\" IS NOT NULL");

        builder.Property(u => u.UniversityId).HasColumnName("university_id").HasColumnType("citext");
        builder.HasIndex(u => u.UniversityId).IsUnique().HasDatabaseName("ux_users_university_id").HasFilter("\"university_id\" IS NOT NULL");

        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.SuspendedAt).HasColumnName("suspended_at");

        builder.OwnsOne(u => u.Name, name =>
        {
            name.Property(n => n.GivenName).HasColumnName("given_name").IsRequired();
            name.Property(n => n.FamilyName).HasColumnName("family_name").IsRequired();
            name.Property(n => n.GivenNameBn).HasColumnName("given_name_bn");
            name.Property(n => n.FamilyNameBn).HasColumnName("family_name_bn");
        });
        builder.Navigation(u => u.Name).IsRequired();

        builder.OwnsOne(u => u.Credential, credential =>
        {
            credential.Property(c => c.PasswordHash).HasColumnName("password_hash").IsRequired();
            credential.Property(c => c.Algorithm).HasColumnName("password_algorithm").IsRequired();
            credential.Property(c => c.ChangedAt).HasColumnName("password_changed_at").IsRequired();
        });
        builder.Navigation(u => u.Credential).IsRequired();

        builder.OwnsMany(u => u.RoleAssignments, assignment =>
        {
            assignment.ToTable("user_role_assignments");
            assignment.WithOwner().HasForeignKey("UserId");
            assignment.HasKey(a => a.Id);
            assignment.Property(a => a.Id)
                .HasConversion(id => id.Value, value => new UserRoleAssignmentId(value))
                .ValueGeneratedNever()
                .HasColumnName("id");
            assignment.Property(a => a.RoleId)
                .HasConversion(id => id.Value, value => new RoleId(value))
                .HasColumnName("role_id");
            assignment.Property(a => a.ScopeNode)
                .HasConversion(
                    node => node.HasValue ? node.Value.Value : (Guid?)null,
                    value => value.HasValue ? new OrganizationNodeId(value.Value) : (OrganizationNodeId?)null)
                .HasColumnName("organization_node_id");
            assignment.Property(a => a.AssignedAt).HasColumnName("assigned_at").IsRequired();
            assignment.Property(a => a.RevokedAt).HasColumnName("revoked_at");
            assignment.HasIndex("UserId", "RoleId", "ScopeNode").HasDatabaseName("ix_user_role_assignments_user_role_scope");
        });
        builder.Navigation(u => u.RoleAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
