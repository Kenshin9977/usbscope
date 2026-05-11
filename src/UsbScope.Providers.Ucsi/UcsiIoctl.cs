using System.Runtime.InteropServices;

namespace UsbScope.Providers.Ucsi;

/// IOCTL contract with the UsbScope kernel driver. Mirrors
/// `src/UsbScope.Driver/Public.h` — kept in sync by hand.
///
/// CTL_CODE(FILE_DEVICE_UNKNOWN=0x22, fn, METHOD_BUFFERED=0, FILE_READ_ACCESS=1)
/// = (0x22 << 16) | (1 << 14) | (fn << 2) | 0
///
/// Function 0x801 → 0x226004 (PING)
/// Function 0x802 → 0x226008 (GET_UCSI_STATE)
/// Function 0x803 → 0x22600C (GET_DISCOVER_IDENTITY)
internal static class UcsiIoctl
{
    public const string UserDevicePath = @"\\.\UsbScope";

    public const uint IOCTL_USBSCOPE_PING                  = 0x00226004;
    public const uint IOCTL_USBSCOPE_GET_UCSI_STATE        = 0x00226008;
    public const uint IOCTL_USBSCOPE_GET_DISCOVER_IDENTITY = 0x0022600C;

    public const uint USBSCOPE_DRIVER_MAJOR = 1;

    [Flags]
    public enum DriverCapabilities : uint
    {
        None         = 0,
        UcsiState    = 1u << 0,
        DiscoverId   = 1u << 1,
    }

    public static byte MajorVersion(uint v) => (byte)((v >> 16) & 0xFF);
    public static byte MinorVersion(uint v) => (byte)(v & 0xFFFF);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USBSCOPE_PING_RESPONSE
{
    public uint Version;
    public uint Capabilities;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USBSCOPE_CONNECTOR_STATUS
{
    public byte ConnectorIndex;
    /// UCSI PowerOperationMode: 0=Unknown, 1=USB Default, 2=BC, 3=PD, 4=Type-C 1.5A, 5=Type-C 3.0A
    public byte PowerOperationMode;
    public byte ConnectStatus;
    /// 0=sink, 1=source
    public byte PowerDirection;
    public uint NegotiatedVoltageMv;
    public uint NegotiatedCurrentMa;
    /// UCSI PartnerType: 1=DFP, 2=UFP, 3=Cable+UFP, 4=Cable+nothing, 5=Debug, 6=Audio
    public byte PartnerType;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USBSCOPE_UCSI_STATE
{
    public byte NumberOfConnectors;
    public byte UcsiVersionMajor;
    public byte UcsiVersionMinor;
    public byte Reserved;

    // Inline fixed-size array of 8 connectors. C# 13: InlineArray would
    // be cleaner; we keep individual fields for compatibility.
    public USBSCOPE_CONNECTOR_STATUS Connector0;
    public USBSCOPE_CONNECTOR_STATUS Connector1;
    public USBSCOPE_CONNECTOR_STATUS Connector2;
    public USBSCOPE_CONNECTOR_STATUS Connector3;
    public USBSCOPE_CONNECTOR_STATUS Connector4;
    public USBSCOPE_CONNECTOR_STATUS Connector5;
    public USBSCOPE_CONNECTOR_STATUS Connector6;
    public USBSCOPE_CONNECTOR_STATUS Connector7;

    public USBSCOPE_CONNECTOR_STATUS this[int index] => index switch
    {
        0 => Connector0, 1 => Connector1, 2 => Connector2, 3 => Connector3,
        4 => Connector4, 5 => Connector5, 6 => Connector6, 7 => Connector7,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USBSCOPE_DISCOVER_IDENTITY
{
    public byte ConnectorIndex;
    /// 0=ACK, 1=NAK, 2=BUSY, 3=no response
    public byte ResponseType;
    public byte Reserved0;
    public byte Reserved1;
    public uint IdHeaderVdo;
    public uint CertStatVdo;
    public uint ProductVdo;
    public uint ProductTypeVdo0;
    public uint ProductTypeVdo1;
    public uint ProductTypeVdo2;
}
