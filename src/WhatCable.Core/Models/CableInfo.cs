namespace WhatCable.Core.Models;

/// Decoded e-marker information for an active or passive USB-C cable.
/// In Phase 1 everything here is null — we surface the type as
/// "Unknown cable" in the UI. Phase 3 fills this in by parsing the
/// Discover Identity VDO response from UCSI 2.0+.
public sealed record CableInfo
{
    public CableType Type { get; init; } = CableType.Unknown;
    public UsbDataRate MaxDataRate { get; init; } = UsbDataRate.Unknown;
    public int? MaxCurrentMa { get; init; }
    public int? MaxVoltageMv { get; init; }
    public ushort? VendorId { get; init; }
    public string? VendorName { get; init; }
    public bool? IsThunderbolt { get; init; }
    public bool? SupportsDisplayPortAltMode { get; init; }
    public byte? UsbPdRevisionMajor { get; init; }
    public byte? UsbPdRevisionMinor { get; init; }
}

public enum CableType
{
    Unknown,
    PassiveUsb2,
    PassiveUsb3,
    ActiveOptical,
    ActiveRedrivenOrRetimed,
    ThunderboltPassive,
    ThunderboltActive,
}
