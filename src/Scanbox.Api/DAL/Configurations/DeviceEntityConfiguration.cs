using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scanbox.Api.DAL.Entities;

namespace Scanbox.Api.DAL.Configurations;

public sealed class DeviceEntityConfiguration : IEntityTypeConfiguration<DeviceEntity>
{
    public void Configure(EntityTypeBuilder<DeviceEntity> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.MacAddress)
            .HasMaxLength(17)
            .IsRequired();

        builder.HasIndex(x => x.MacAddress)
            .IsUnique();

        builder.Property(x => x.PairingCode)
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(x => x.ClassName)
            .HasMaxLength(16);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastSeenAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasMany(x => x.TagMappings)
            .WithOne(x => x.Device)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
