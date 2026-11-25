namespace Scanbox.Api.DAL.Entities;

public sealed class DeviceEntity
{
    public Guid Id { get; set; }

    public string MacAddress { get; set; } = string.Empty;

    public string PairingCode { get; set; } = string.Empty;

    public string? ClassName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public ICollection<TagMappingEntity> TagMappings { get; set; } = new List<TagMappingEntity>();
}
