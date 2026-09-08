using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Research.Domain.FundingBodies;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

internal sealed class FundingBodyConfiguration : IEntityTypeConfiguration<FundingBody>
{
    public void Configure(EntityTypeBuilder<FundingBody> builder)
    {
        builder.ToTable("funding_bodies");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasConversion(id => id.Value, value => new FundingBodyId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(f => f.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
        builder.Property(f => f.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Website).HasColumnName("website").HasMaxLength(500);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
    }
}
