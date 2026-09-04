using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Faculty.Domain.ResearchProfiles;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Configurations;

internal sealed class ResearchProfileConfiguration : IEntityTypeConfiguration<ResearchProfile>
{
    public void Configure(EntityTypeBuilder<ResearchProfile> builder)
    {
        builder.ToTable("research_profiles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new ResearchProfileId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(r => r.FacultyMemberId).HasColumnName("faculty_member_id").IsRequired();
        builder.Property(r => r.OngoingResearch).HasColumnName("ongoing_research");
        builder.Property(r => r.Grants).HasColumnName("grants");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(r => r.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(r => r.FacultyMemberId).IsUnique().HasDatabaseName("ux_research_profiles_faculty_member_id");

        builder.OwnsMany(r => r.Publications, publication =>
        {
            publication.ToTable("research_profile_publications");
            publication.WithOwner().HasForeignKey("ResearchProfileId");
            publication.Property<int>("Id").ValueGeneratedOnAdd();
            publication.HasKey("ResearchProfileId", "Id");
            publication.Property<ResearchProfileId>("ResearchProfileId")
                .HasConversion(id => id.Value, value => new ResearchProfileId(value))
                .HasColumnName("research_profile_id");
            publication.Property(p => p.Title).HasColumnName("title").IsRequired();
            publication.Property(p => p.Venue).HasColumnName("venue").IsRequired();
            publication.Property(p => p.Year).HasColumnName("year").IsRequired();
            publication.Property(p => p.Url).HasColumnName("url");
        });
        builder.Navigation(r => r.Publications).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
