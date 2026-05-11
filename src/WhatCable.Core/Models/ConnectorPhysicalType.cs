namespace WhatCable.Core.Models;

/// Physical shape of a USB receptacle. Independent of what's negotiated
/// over the link — a USB-C port can carry USB 2.0, a USB-A port can
/// carry USB 3.2, etc.
///
/// We surface this so the UI knows which features to expose: USB-C
/// rows can show PD / e-marker / Alt Mode info; USB-A rows can't.
public enum ConnectorPhysicalType
{
    Unknown,
    UsbTypeA,
    UsbTypeB,
    UsbMiniB,
    UsbMicroB,
    UsbTypeC,
    /// Internal header on the motherboard, not user-accessible.
    Internal,
}

public static class ConnectorPhysicalTypeExtensions
{
    /// True when this port can negotiate USB Power Delivery and expose
    /// e-marker / Alt Mode data. Used to gate USB-C-specific UI and
    /// providers.
    public static bool SupportsUsbPd(this ConnectorPhysicalType type)
        => type == ConnectorPhysicalType.UsbTypeC;
}
