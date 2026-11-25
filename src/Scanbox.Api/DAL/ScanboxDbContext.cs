using Microsoft.EntityFrameworkCore;
using Scanbox.Api.DAL.Entities;

namespace Scanbox.Api.DAL;

public sealed class ScanboxDbContext(DbContextOptions<ScanboxDbContext> options) : DbContext(options)
{
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();

    public DbSet<TagMappingEntity> TagMappings => Set<TagMappingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScanboxDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
