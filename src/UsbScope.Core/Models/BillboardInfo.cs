namespace UsbScope.Core.Models;

/// USB Billboard Device Class advertisement, as parsed from a USB
/// device's BOS (Binary Device Object Store) descriptor. Tells us what
/// USB-C Alternate Modes a device (typically a dock or adapter)
/// declares it supports — DisplayPort, Thunderbolt, etc.
///
/// Billboard descriptors are readable in pure userspace via the hub
/// IOCTL on any Windows 10/11 machine; UCSI is not required.
public sealed record BillboardInfo
{
    public byte NumberOfAlternateModes { get; init; }
    public byte PreferredAlternateMode { get; init; }
    /// 16-bit BCD version of the Billboard spec the device advertises
    /// against. 0x0121 = "1.21", 0x0110 = "1.1".
    public ushort BcdVersion { get; init; }
    public byte AdditionalFailureInfo { get; init; }
    public IReadOnlyList<AlternateModeAdvert> AlternateModes { get; init; } = [];

    public string BcdVersionString =>
        $"{(BcdVersion >> 8) & 0xFF:X}.{BcdVersion & 0xFF:X2}";
}

public sealed record AlternateModeAdvert
{
    /// 16-bit Standard or Vendor ID (USB-IF assigned). Common values:
    /// 0xFF01 = VESA DisplayPort, 0x8087 = Intel Thunderbolt.
    public ushort Svid { get; init; }
    /// Vendor-defined mode within the SVID. For DisplayPort this is
    /// the pin assignment ID (A=1, B=2, C=3, D=4, E=5, F=6).
    public byte AlternateMode { get; init; }
    /// Resolved name when the SVID is in our registry. Null otherwise.
    public string? SvidName { get; init; }
}
