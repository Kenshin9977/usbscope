namespace WhatCable.Core.Models;

/// A physical USB-C port on the host. One row per port in the UI.
public sealed record UsbPort
{
    public required string PortId { get; init; }
    public string? Label { get; init; }
    public ConnectorRole Role { get; init; } = ConnectorRole.Unknown;
    public ConnectedDevice? Device { get; init; }
    public CableInfo? Cable { get; init; }
    public PowerInfo Power { get; init; } = new();

    /// Free-form provider-specific data (e.g. WMI properties surfaced
    /// behind the engineer-mode toggle). Keep IDs out of normal UI.
    public IReadOnlyDictionary<string, string> RawProperties { get; init; }
        = new Dictionary<string, string>();
}
