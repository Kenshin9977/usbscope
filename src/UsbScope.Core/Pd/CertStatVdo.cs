namespace UsbScope.Core.Pd;

/// USB-PD Discover Identity — Cert Stat VDO. Layout per USB-PD 3.1
/// §6.4.4.3.1.2, Table 6-30. Carries the USB-IF certification ID
/// (XID) assigned to the product, or 0 if the product is not USB-IF
/// certified.
public readonly record struct CertStatVdo(uint Raw)
{
    /// Bits 31:0 — XID (USB-IF certification ID, 32-bit). 0 means
    /// uncertified.
    public uint Xid => Raw;
    public bool IsUsbIfCertified => Raw != 0;
}
