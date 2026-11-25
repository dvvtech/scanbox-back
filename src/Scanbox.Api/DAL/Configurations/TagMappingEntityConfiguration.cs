using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scanbox.Api.DAL.Entities;

namespace Scanbox.Api.DAL.Configurations;

public sealed class TagMappingEntityConfiguration : IEntityTypeConfiguration<TagMappingEntity>
{
    public void Configure(EntityTypeBuilder<TagMappingEntity> builder)
    {
        builder.ToTable("device_tag_mappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.RfidTag)
            .HasMaxLength(62)
            .IsRequired();

        builder.HasIndex(x => new { x.DeviceId, x.SubjectId })
            .IsUnique();

        builder.HasIndex(x => new { x.DeviceId, x.RfidTag })
            .IsUnique();
    }
}
