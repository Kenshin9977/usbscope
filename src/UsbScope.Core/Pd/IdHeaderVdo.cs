namespace UsbScope.Core.Pd;

/// USB-PD Discover Identity response — ID Header VDO.
/// Layout per USB Power Delivery 3.1 §6.4.4.3.1.1, Table 6-29.
///
/// This is the first VDO in a Discover Identity ACK and tells us the
/// vendor and what kind of product we're talking to (hub, peripheral,
/// cable, etc.). Subsequent VDOs are typed based on this header.
public readonly record struct IdHeaderVdo(uint Raw)
{
    /// Bit 31 — device can act as a USB host (DFP-D communications).
    public bool UsbCommunicationsCapableAsHost  => (Raw & (1u << 31)) != 0;

    /// Bit 30 — device can act as a USB peripheral (UFP-D communications).
    public bool UsbCommunicationsCapableAsDevice => (Raw & (1u << 30)) != 0;

    /// Bits 29:27 — Product Type (UFP). Set when this device can be a
    /// downstream (peripheral) on USB.
    public ProductTypeUfp ProductTypeUfp => (ProductTypeUfp)((Raw >> 27) & 0b111);

    /// Bit 26 — Modal Operation Supported (i.e., Alt Mode capable).
    public bool ModalOperationSupported => (Raw & (1u << 26)) != 0;

    /// Bits 25:23 — Product Type (DFP). Set when this device can be a
    /// host on USB.
    public ProductTypeDfp ProductTypeDfp => (ProductTypeDfp)((Raw >> 23) & 0b111);

    /// Bits 22:21 — Connector Type. Added in USB-PD 3.0.
    public ConnectorType ConnectorType => (ConnectorType)((Raw >> 21) & 0b11);

    /// Bits 15:0 — USB Vendor ID (USB-IF assigned).
    public ushort VendorId => (ushort)(Raw & 0xFFFF);
}

/// Product Type when this device speaks as a UFP (peripheral).
public enum ProductTypeUfp : byte
{
    Undefined            = 0,
    PdUsbHub             = 1,
    PdUsbPeripheral      = 2,
    /// Power-Sink Device. Devices that primarily consume power.
    Psd                  = 3,
    /// Alternate Mode Adapter — deprecated in PD 3.1.
    AmaDeprecated        = 4,
    /// VCONN-Powered USB Device (active cable bonus chip etc.).
    Vpd                  = 5,
}

/// Product Type when this device speaks as a DFP (host or charger).
public enum ProductTypeDfp : byte
{
    Undefined            = 0,
    PdUsbHub             = 1,
    PdUsbHost            = 2,
    PowerBrick           = 3,
}

/// USB-C connector type the device advertises. PD 3.0 and later.
public enum ConnectorType : byte
{
    Reserved0            = 0,
    Reserved1            = 1,
    UsbTypeCReceptacle   = 2,
    UsbTypeCPlug         = 3,
}
