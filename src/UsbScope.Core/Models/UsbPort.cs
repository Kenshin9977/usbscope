namespace UsbScope.Core.Models;

/// A physical USB port on the host. One row per port in the UI.
/// Used for all USB shapes (Type-A, Type-B, Type-C, internal headers);
/// `PhysicalType` discriminates so USB-C-specific features can be
/// gated to actual USB-C rows.
public sealed record UsbPort
{
    public required string PortId { get; init; }
    public string? Label { get; init; }
    public ConnectorPhysicalType PhysicalType { get; init; } = ConnectorPhysicalType.Unknown;
    public ConnectorRole Role { get; init; } = ConnectorRole.Unknown;
    public ConnectedDevice? Device { get; init; }
    public CableInfo? Cable { get; init; }
    public PowerInfo Power { get; init; } = new();

    /// Free-form provider-specific data (e.g. WMI properties surfaced
    /// behind the engineer-mode toggle). Keep IDs out of normal UI.
    public IReadOnlyDictionary<string, string> RawProperties { get; init; }
        = new Dictionary<string, string>();
}
