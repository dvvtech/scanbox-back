namespace Scanbox.Api.DAL.Entities;

public sealed class TagMappingEntity
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public DeviceEntity Device { get; set; } = null!;

    public int SubjectId { get; set; }

    public string RfidTag { get; set; } = string.Empty;
}
