using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UsbScope.Providers.Windows.Interop;

/// IOCTL constants and native structs for the USB hub user-mode
/// interface (usbioctl.h / usbuser.h / usbspec.h). Hand-written
/// because CsWin32's metadata is inconsistent for the USB stack —
/// some structs are present, some are missing, and the IOCTL macro
/// expansions are not always exposed.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class UsbIoctl
{
    // CTL_CODE(FILE_DEVICE_USB=0x22, function, METHOD_BUFFERED=0, FILE_ANY_ACCESS=0)
    // = (0x22 << 16) | (function << 2)
    public const uint IOCTL_USB_GET_NODE_INFORMATION                    = 0x00220408;
    public const uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX      = 0x00220448;
    public const uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2   = 0x00220460;
    public const uint IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION     = 0x0022040C;

    // USB_DEVICE_SPEED (usbspec.h)
    public const byte UsbLowSpeed       = 0;
    public const byte UsbFullSpeed      = 1;
    public const byte UsbHighSpeed      = 2;
    public const byte UsbSuperSpeed     = 3;

    // USB descriptor types (usbspec.h)
    public const byte USB_DEVICE_DESCRIPTOR_TYPE = 0x01;
    public const byte USB_BOS_DESCRIPTOR_TYPE    = 0x0F;

    // USB_DEVICE_CAPABILITY_TYPE (usbspec.h)
    public const byte USB_DEVICE_CAPABILITY_BILLBOARD = 0x0D;
}

/// USB 2.0/3.x device descriptor (usbspec.h). Total 18 bytes, pack=1.
[SupportedOSPlatform("windows10.0.22621.0")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USB_DEVICE_DESCRIPTOR
{
    public byte bLength;
    public byte bDescriptorType;
    public ushort bcdUSB;
    public byte bDeviceClass;
    public byte bDeviceSubClass;
    public byte bDeviceProtocol;
    public byte bMaxPacketSize0;
    public ushort idVendor;
    public ushort idProduct;
    public ushort bcdDevice;
    public byte iManufacturer;
    public byte iProduct;
    public byte iSerialNumber;
    public byte bNumConfigurations;
}

/// USB_NODE_CONNECTION_INFORMATION_EX (usbioctl.h). Pack=1, total
/// 35 bytes before the PipeList[] flexible array. We never read the
/// PipeList — we don't care about endpoint info at this layer.
[SupportedOSPlatform("windows10.0.22621.0")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USB_NODE_CONNECTION_INFORMATION_EX
{
    public uint ConnectionIndex;
    public USB_DEVICE_DESCRIPTOR DeviceDescriptor;
    public byte CurrentConfigurationValue;
    public byte Speed;
    public byte DeviceIsHub;
    public ushort DeviceAddress;
    public uint NumberOfOpenPipes;
    public uint ConnectionStatus;
    // USB_PIPE_INFO PipeList[0] — not read.
}

/// USB_NODE_INFORMATION (usbioctl.h). Pack=1. We only read the
/// `bNumberOfPorts` field of the hub descriptor inside it, so we
/// declare a minimal layout — enough bytes to reach that offset.
[SupportedOSPlatform("windows10.0.22621.0")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USB_NODE_INFORMATION_MINIMAL
{
    public uint NodeType;                  // USB_HUB_NODE: 0 = UsbHub
    public byte HubDescriptor_bLength;
    public byte HubDescriptor_bDescriptorType;
    public byte HubDescriptor_bNumberOfPorts;
    // remaining hub descriptor fields ignored
}

/// USB_DESCRIPTOR_REQUEST (usbioctl.h). Wraps a USB SETUP packet
/// plus the destination buffer for an IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION
/// call. Pack=1.
[SupportedOSPlatform("windows10.0.22621.0")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct USB_DESCRIPTOR_REQUEST_HEADER
{
    public uint ConnectionIndex;
    // USB SETUP packet — 8 bytes
    public byte bmRequest;
    public byte bRequest;
    public ushort wValue;
    public ushort wIndex;
    public ushort wLength;
    // followed by the requested descriptor bytes (Data[0])
}
