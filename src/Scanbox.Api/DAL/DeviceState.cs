namespace Scanbox.Api.DAL;

public sealed record RfidTagMapping(int SubjectId, string RfidTag);

public sealed record DeviceState(
    string MacAddress,
    string PairingCode,
    string ClassName,
    IReadOnlyList<RfidTagMapping> TagMappings);
