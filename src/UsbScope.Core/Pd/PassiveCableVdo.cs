namespace UsbScope.Core.Pd;

/// USB-PD Discover Identity — Passive Cable VDO. Layout per USB-PD 3.1
/// §6.4.4.3.1.5, Table 6-33. Returned when the device is a passive
/// USB-C cable (the e-marker chip in the plug).
///
/// This is the key VDO for WhatCable-style decoding: it tells us
/// what the *cable* is rated for — VBUS current, USB speed, cable
/// type — independent of what the host or charger can do.
public readonly record struct PassiveCableVdo(uint Raw)
{
    /// Bits 31:28 — HW Version. Vendor-specific.
    public byte HwVersion => (byte)((Raw >> 28) & 0xF);

    /// Bits 27:24 — Firmware Version. Vendor-specific.
    public byte FirmwareVersion => (byte)((Raw >> 24) & 0xF);

    /// Bits 23:21 — VDO Version. 0 = 1.0, 1 = 1.3, etc.
    public byte VdoVersion => (byte)((Raw >> 21) & 0b111);

    /// Bits 20:18 — USB-PD Major Revision supported by the cable.
    /// PD 3.0/3.1 cables typically report 3.
    public byte PdMajorRevision => (byte)((Raw >> 18) & 0b111);

    /// Bits 17:14 — Cable USB Type (Passive vs Active vs VPD).
    /// Bits 11:9 — Plug Type at the second end. 0=A, 1=B, 2=C, 3=captive.
    /// We expose them as enums.

    /// Bits 13:11 — USB Highest Speed advertised on the cable.
    public CableUsbSpeed UsbHighestSpeed => (CableUsbSpeed)((Raw >> 11) & 0b111);

    /// Bits 10:9 — VBUS Current Handling Capability.
    public CableVbusCurrent VbusCurrent => (CableVbusCurrent)((Raw >> 9) & 0b11);

    /// Bit 8 — SBU Type. 0 = passive, 1 = active.
    public bool SbuActive => (Raw & (1u << 8)) != 0;

    /// Bits 6:5 — Max VBUS Voltage. 0=20V, 1=30V, 2=40V, 3=50V.
    public CableVbusMaxVoltage VbusMaxVoltage => (CableVbusMaxVoltage)((Raw >> 5) & 0b11);

    /// Bits 4:3 — Cable Termination Type. 00=Passive, 01=Active w/ comms,
    /// 10/11 reserved in PD 3.1 for older active cables.
    public CableTerminationType TerminationType => (CableTerminationType)((Raw >> 3) & 0b11);

    /// Bits 2:0 — Cable Latency, in 10 ns units. 1 = ≤10 ns, 2 = ≤20 ns,
    /// etc., up to 7 = >5000 ns. Useful for diagnosing long cables.
    public byte LatencyCode => (byte)(Raw & 0b111);

    public int? LatencyNanosecondsApprox => LatencyCode switch
    {
        0 => null,           // reserved
        1 => 10,
        2 => 20,
        3 => 30,
        4 => 40,
        5 => 100,
        6 => 1_000,
        7 => 5_000,          // > 5000 ns; we cap at 5000
        _ => null,
    };
}

public enum CableUsbSpeed : byte
{
    Usb2Only            = 0,
    Usb3_2Gen1          = 1,  // 5 Gbps
    Usb3_2Gen2          = 2,  // 10 Gbps
    Usb4Gen3            = 3,  // 20 Gbps per pair
    Usb4Gen4            = 4,  // 40 Gbps per pair (USB4 v2)
    Reserved5           = 5,
    Reserved6           = 6,
    Reserved7           = 7,
}

public enum CableVbusCurrent : byte
{
    Reserved            = 0,
    ThreeAmps           = 1,  // 3 A
    FiveAmps            = 2,  // 5 A — required for >60W cables
    Reserved3           = 3,
}

public enum CableVbusMaxVoltage : byte
{
    TwentyVolts         = 0,
    ThirtyVolts         = 1,
    FortyVolts          = 2,
    FiftyVolts          = 3,
}

public enum CableTerminationType : byte
{
    Passive             = 0,
    ActiveWithComms     = 1,
    Reserved2           = 2,
    Reserved3           = 3,
}
