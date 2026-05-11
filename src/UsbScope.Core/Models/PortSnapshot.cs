namespace UsbScope.Core.Models;

/// One frozen read of the USB subsystem, suitable for JSON
/// serialization (CLI output) or binding to the UI.
///
/// `Ports` and `Devices` are two views on the same world:
///   - `Ports` is the host's physical receptacles (from SMBIOS).
///   - `Devices` is everything currently negotiated on the USB bus.
/// In Phase 1 we don't reliably correlate the two on every machine —
/// hub topology + SMBIOS designators don't share a key — so we surface
/// both and let the UI render them side-by-side. When a port provider
/// *can* attribute a device to its port (e.g. WmiPortProvider on UCM
/// systems), it sets UsbPort.Device and that device also appears in
/// the flat Devices list.
public sealed record PortSnapshot
{
    public required DateTimeOffset CapturedAt { get; init; }
    public required string HostMachine { get; init; }
    public required IReadOnlyList<UsbPort> Ports { get; init; }
    public IReadOnlyList<ConnectedDevice> Devices { get; init; } = [];
    public IReadOnlyList<string> ProviderNames { get; init; } = [];

    /// Soft warnings: a provider was queried, threw, and we degraded.
    /// Useful in --json and in engineer mode.
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
