namespace Scanbox.Api.DAL;

public interface IDeviceStateRepository
{
    Task<DeviceState?> GetAsync(string macAddress, CancellationToken cancellationToken);

    Task RegisterConnectionAsync(string macAddress, string pairingCode, CancellationToken cancellationToken);

    Task SaveTagMappingsAsync(
        string macAddress,
        string pairingCode,
        IReadOnlyCollection<RfidTagMapping> mappings,
        CancellationToken cancellationToken);

    Task SaveClassNameAsync(
        string macAddress,
        string pairingCode,
        string className,
        CancellationToken cancellationToken);
}
