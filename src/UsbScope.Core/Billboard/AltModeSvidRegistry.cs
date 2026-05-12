namespace UsbScope.Core.Billboard;

/// Maps USB-IF Standard/Vendor IDs (SVIDs) used in USB-C Alternate
/// Mode advertisements to human-readable names. Sourced from the
/// USB-IF SVID registry (https://www.usb.org/sites/default/files/vid_usage_2025.pdf
/// and similar). Kept small on purpose — we only need names for the
/// Alt Modes a desktop user is likely to plug in.
public static class AltModeSvidRegistry
{
    public static string? Resolve(ushort svid) => svid switch
    {
        0xFF01 => "VESA DisplayPort",
        0x8087 => "Intel Thunderbolt",
        0x18D1 => "Google",
        0x05AC => "Apple",
        0x0451 => "Texas Instruments",
        0x17EF => "Lenovo",
        0x413C => "Dell",
        0x03F0 => "HP",
        0x04E8 => "Samsung",
        0x0CF3 => "Qualcomm",
        _ => null,
    };

    /// Friendly description that includes the SVID hex so users can
    /// look up vendor-specific Alt Modes the registry doesn't cover.
    public static string Describe(ushort svid)
    {
        var name = Resolve(svid);
        return name is null ? $"SVID 0x{svid:X4}" : $"{name} (0x{svid:X4})";
    }
}
