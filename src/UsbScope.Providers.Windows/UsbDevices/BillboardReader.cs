using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using UsbScope.Core.Billboard;
using UsbScope.Core.Models;
using UsbScope.Providers.Windows.Interop;

namespace UsbScope.Providers.Windows.UsbDevices;

/// Reads the BOS descriptor of a USB device through its parent hub
/// and extracts the Billboard capability when present. Uses
/// IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, the same kernel
/// entry point USBView and Linux usb-utils rely on. No driver
/// required, works on Windows 10 / 11 universally.
///
/// Two-step protocol: first request 5 bytes to discover wTotalLength,
/// then request the full BOS. Most devices return short BOS (<128 B)
/// so a single 1024-byte buffer is fine in practice — we still do
/// the two-step to be polite to devices that report partial data.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class BillboardReader
{
    private const byte  GET_DESCRIPTOR        = 0x06;
    private const byte  BOS_DESCRIPTOR_TYPE   = 0x0F;
    private const byte  REQUEST_TYPE_GET_DESC = 0x80; // D2H | Standard | Device
    private const ushort BOS_HEADER_LENGTH    = 5;
    private const int   MAX_BOS_LENGTH        = 4096;

    public static BillboardInfo? TryRead(SafeFileHandle hub, uint portNumber)
    {
        // Step 1: read just the BOS header to find out the total length.
        var header = ReadBosFragment(hub, portNumber, BOS_HEADER_LENGTH);
        if (header is null || header.Length < 5) return null;
        if (header[1] != BOS_DESCRIPTOR_TYPE) return null;

        ushort totalLength = (ushort)(header[2] | (header[3] << 8));
        if (totalLength < 5 || totalLength > MAX_BOS_LENGTH) return null;

        // Step 2: read the full BOS now we know the size.
        var full = ReadBosFragment(hub, portNumber, totalLength);
        if (full is null || full.Length < totalLength) return null;

        return BillboardParser.TryParse(full.AsSpan(0, totalLength));
    }

    /// Issues IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION for the BOS
    /// descriptor and returns the raw bytes (just the descriptor, the
    /// USB_DESCRIPTOR_REQUEST header is stripped). Null on failure.
    private static unsafe byte[]? ReadBosFragment(SafeFileHandle hub, uint portNumber, ushort requestedLength)
    {
        var headerSize = Marshal.SizeOf<USB_DESCRIPTOR_REQUEST_HEADER>();
        var totalSize  = headerSize + requestedLength;
        var buffer     = new byte[totalSize];

        fixed (byte* p = buffer)
        {
            var req = (USB_DESCRIPTOR_REQUEST_HEADER*)p;
            req->ConnectionIndex = portNumber;
            req->bmRequest       = REQUEST_TYPE_GET_DESC;
            req->bRequest        = GET_DESCRIPTOR;
            req->wValue          = (ushort)(BOS_DESCRIPTOR_TYPE << 8);
            req->wIndex          = 0;
            req->wLength         = requestedLength;

            uint bytesReturned = 0;
            var ok = PInvoke.DeviceIoControl(
                hub,
                UsbIoctl.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                p, (uint)totalSize,
                p, (uint)totalSize,
                &bytesReturned,
                null);

            if (!ok || bytesReturned <= headerSize)
                return null;

            var dataLength = (int)bytesReturned - headerSize;
            var result = new byte[dataLength];
            Array.Copy(buffer, headerSize, result, 0, dataLength);
            return result;
        }
    }
}
