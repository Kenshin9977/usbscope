using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using UsbScope.Core.Models;
using UsbScope.Core.UsbIds;
using UsbScope.Providers.Windows.Interop;
using UsbScope.Providers.Windows.Wmi;

namespace UsbScope.Providers.Windows.UsbDevices;

/// Phase 1 device enumeration. Returns every USB device the OS sees
/// — independent of UCM, vendor PD stacks, or any USB-C-specific
/// gating. Data comes from three universally-available sources:
///   - Win32_PnPEntity with DeviceID starting in `USB\` (the device list)
///   - DEVPKEY_Device_UsbSpeed via cfgmgr32 (authoritative negotiated speed)
///   - UsbIdsDatabase (community DB) for vendor + product names
///
/// We deliberately do not attempt port correlation here. That requires
/// either UCSI (Phase 2) or hub-topology IOCTLs (later iteration); the
/// flat list is the right Phase 1 answer because it is honest about
/// what we can reliably know in pure userspace.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class UsbDeviceEnumerator
{
    public static async IAsyncEnumerable<ConnectedDevice> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // We match by DeviceID prefix rather than PNPClass='USB' because
        // composite USB devices (a USB headset that registers as Audio +
        // HID + ...) live under PNPClass='Audio' etc. but their DeviceID
        // still starts with USB\, and we want to surface them too.
        const string wql = """
            SELECT DeviceID, Name, Description, Manufacturer, ClassGuid,
                   HardwareID, PNPClass, Service, Status
            FROM Win32_PnPEntity
            WHERE DeviceID LIKE 'USB\\%'
            """;

        foreach (var row in WmiQueryHelper.Query(@"\\.\root\cimv2", wql))
        {
            ct.ThrowIfCancellationRequested();

            var instanceId = WmiQueryHelper.GetString(row, "DeviceID");
            if (string.IsNullOrEmpty(instanceId))
                continue;

            // Skip hubs and root composite-device pseudo-entries. A USB
            // hub is a real USB device, but listing it as a "device"
            // clutters the view — we already surface its presence as
            // part of the topology when devices are attached to it.
            var service = WmiQueryHelper.GetString(row, "Service");
            if (string.Equals(service, "USBHUB3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(service, "usbhub", StringComparison.OrdinalIgnoreCase))
                continue;

            var (vid, pid) = ParseVidPidFromInstanceId(instanceId);

            // Prefer usb.ids names over WMI strings: WMI returns whatever
            // the driver chose to ship (frequently empty or a generic
            // "USB Composite Device"), while usb.ids is community-vetted.
            string? vendorName = vid is ushort vidValue
                ? UsbIdsDatabase.Instance.ResolveVendor(vidValue)
                : null;
            vendorName ??= WmiQueryHelper.GetString(row, "Manufacturer");

            string? productName = (vid, pid) is (ushort v, ushort p)
                ? UsbIdsDatabase.Instance.ResolveProduct(v, p)
                : null;
            productName ??= WmiQueryHelper.GetString(row, "Name")
                ?? WmiQueryHelper.GetString(row, "Description");

            yield return new ConnectedDevice
            {
                InstanceId = instanceId,
                FriendlyName = productName,
                Manufacturer = vendorName,
                VendorId = vid,
                ProductId = pid,
                NegotiatedRate = UsbSpeedReader.Read(instanceId),
                DeviceClass = WmiQueryHelper.GetString(row, "PNPClass"),
            };

            await Task.Yield();
        }
    }

    /// USB device instance IDs follow the format
    /// `USB\VID_05E3&PID_0732\serial`. The first segment is parseable
    /// without WMI's HardwareID array, which is more reliable across
    /// machines.
    private static (ushort? Vid, ushort? Pid) ParseVidPidFromInstanceId(string instanceId)
    {
        ushort? vid = ExtractHex(instanceId, "VID_");
        ushort? pid = ExtractHex(instanceId, "PID_");
        return (vid, pid);
    }

    private static ushort? ExtractHex(string source, string prefix)
    {
        var idx = source.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + prefix.Length;
        if (start + 4 > source.Length) return null;
        return ushort.TryParse(
            source.AsSpan(start, 4),
            System.Globalization.NumberStyles.HexNumber,
            null,
            out var v) ? v : null;
    }
}
