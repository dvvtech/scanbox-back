using Microsoft.EntityFrameworkCore;
using Scanbox.Api.DAL.Entities;

namespace Scanbox.Api.DAL;

public sealed class DeviceStateRepository(IDbContextFactory<ScanboxDbContext> contextFactory) : IDeviceStateRepository
{
    public async Task<DeviceState?> GetAsync(string macAddress, CancellationToken cancellationToken)
    {
        await using ScanboxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Devices
            .AsNoTracking()
            .Where(device => device.MacAddress == macAddress)
            .Select(device => new DeviceState(
                device.MacAddress,
                device.PairingCode,
                device.ClassName ?? string.Empty,
                device.TagMappings
                    .OrderBy(mapping => mapping.SubjectId)
                    .Select(mapping => new RfidTagMapping(mapping.SubjectId, mapping.RfidTag))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RegisterConnectionAsync(string macAddress, string pairingCode, CancellationToken cancellationToken)
    {
        await using ScanboxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DeviceEntity device = await GetOrCreateDeviceAsync(db, macAddress, pairingCode, now, cancellationToken);

        device.LastSeenAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTagMappingsAsync(
        string macAddress,
        string pairingCode,
        IReadOnlyCollection<RfidTagMapping> mappings,
        CancellationToken cancellationToken)
    {
        await using ScanboxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DeviceEntity device = await GetOrCreateDeviceAsync(db, macAddress, pairingCode, now, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.TagMappings
            .Where(mapping => mapping.DeviceId == device.Id)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (RfidTagMapping mapping in mappings)
        {
            db.TagMappings.Add(new TagMappingEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                SubjectId = mapping.SubjectId,
                RfidTag = mapping.RfidTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveClassNameAsync(
        string macAddress,
        string pairingCode,
        string className,
        CancellationToken cancellationToken)
    {
        await using ScanboxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DeviceEntity device = await GetOrCreateDeviceAsync(db, macAddress, pairingCode, now, cancellationToken);

        device.ClassName = string.IsNullOrWhiteSpace(className) ? null : className;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<DeviceEntity> GetOrCreateDeviceAsync(
        ScanboxDbContext db,
        string macAddress,
        string pairingCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DeviceEntity? device = await db.Devices
            .SingleOrDefaultAsync(existing => existing.MacAddress == macAddress, cancellationToken);

        if (device is null)
        {
            device = new DeviceEntity
            {
                Id = Guid.NewGuid(),
                MacAddress = macAddress,
                PairingCode = pairingCode,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Devices.Add(device);
            return device;
        }

        // The pairing code is stored on the device. If it changed, the data cached
        // for the previous code (class and RFID mappings) no longer belongs to it.
        if (!string.Equals(device.PairingCode, pairingCode, StringComparison.Ordinal))
        {
            device.PairingCode = pairingCode;
            device.ClassName = null;

            await db.TagMappings
                .Where(mapping => mapping.DeviceId == device.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        device.UpdatedAtUtc = now;
        return device;
    }
}
