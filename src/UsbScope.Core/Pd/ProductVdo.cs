namespace UsbScope.Core.Pd;

/// USB-PD Discover Identity — Product VDO. Layout per USB-PD 3.1
/// §6.4.4.3.1.3, Table 6-31. Combines USB Product ID with bcdDevice
/// (device release number, BCD-encoded).
public readonly record struct ProductVdo(uint Raw)
{
    public ushort ProductId => (ushort)((Raw >> 16) & 0xFFFF);
    /// BCD-encoded device release. e.g. 0x0123 = "version 1.23".
    public ushort BcdDevice => (ushort)(Raw & 0xFFFF);

    public string BcdDeviceString
    {
        get
        {
            var major = (BcdDevice >> 8) & 0xFF;
            var minor = BcdDevice & 0xFF;
            return $"{major:X}.{minor:X2}";
        }
    }
}
