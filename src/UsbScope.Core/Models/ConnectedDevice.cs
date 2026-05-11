namespace UsbScope.Core.Models;

/// One device currently visible on a USB-C port. Identified by the
/// Windows device instance ID so we can correlate across providers
/// (SetupAPI, WMI, vendor) without leaking the path into the UI.
public sealed record ConnectedDevice
{
    public required string InstanceId { get; init; }
    public string? FriendlyName { get; init; }
    public string? Manufacturer { get; init; }
    public ushort? VendorId { get; init; }
    public ushort? ProductId { get; init; }
    public UsbDataRate NegotiatedRate { get; init; } = UsbDataRate.Unknown;
    public string? DeviceClass { get; init; }
}
