namespace UsbScope.Core.Models;

/// One bus that a USB-C connection can be running. We surface this as
/// plain English in the UI ("USB 3.2 Gen 2x2 — 20 Gbps").
public enum UsbDataRate
{
    Unknown,
    LowSpeed1_5Mbps,
    FullSpeed12Mbps,
    HighSpeed480Mbps,
    SuperSpeed5Gbps,
    SuperSpeedPlus10Gbps,
    SuperSpeedPlus20Gbps,
    Usb4Gen2x2_20Gbps,
    Usb4Gen3x2_40Gbps,
    Usb4Gen4_80Gbps,
}
