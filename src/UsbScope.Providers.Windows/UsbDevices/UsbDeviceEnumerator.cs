using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using UsbScope.Core.Models;
using UsbScope.Core.UsbIds;
using UsbScope.Providers.Windows.Interop;
using UsbScope.Providers.Windows.Wmi;

namespace UsbScope.Providers.Windows.UsbDevices;

/// Phase 1 device enumeration. Returns every USB device the OS sees
/// — independent of UCM, vendor PD stacks, or any USB-C-specific
/// gating. Data comes from four universally-available sources:
///   - Win32_PnPEntity with DeviceID starting in `USB\` (the device list)
///   - cfgmgr32 for each device's parent hub + port number
///   - hub IOCTLs for authoritative negotiated speed (UsbHubReader)
///   - UsbIdsDatabase (community DB) for vendor + product names
///
/// We deliberately do not attempt port correlation to SMBIOS physical
/// ports here. That requires either UCSI (Phase 2) or OEM-specific
/// hub-port-to-chassis-label mapping; the flat list with parent/port
/// hints is the honest Phase 1 answer.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class UsbDeviceEnumerator
{
    public static async IAsyncEnumerable<ConnectedDevice> EnumerateAsync(
        UsbHubReader hubReader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
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

            // Skip hubs and composite-device sub-interfaces.
            //   - Hubs are real USB devices but they only add noise;
            //     we surface them implicitly as a parent in topology.
            //   - Composite sub-interfaces (USB\VID_x&PID_y&MI_00\...)
            //     are not separate devices on the bus, they're internal
            //     interfaces of one physical thing. The composite parent
            //     is what carries the negotiated speed and port number.
            var service = WmiQueryHelper.GetString(row, "Service");
            if (string.Equals(service, "USBHUB3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(service, "usbhub", StringComparison.OrdinalIgnoreCase))
                continue;
            if (instanceId.Contains("&MI_", StringComparison.OrdinalIgnoreCase)
                || instanceId.Contains("&LAMPARRAY", StringComparison.OrdinalIgnoreCase))
                continue;

            var (vid, pid) = ParseVidPidFromInstanceId(instanceId);

            // Prefer usb.ids over WMI strings.
            string? vendorName = vid is ushort vidValue
                ? UsbIdsDatabase.Instance.ResolveVendor(vidValue)
                : null;
            vendorName ??= WmiQueryHelper.GetString(row, "Manufacturer");

            string? productName = (vid, pid) is (ushort v, ushort p)
                ? UsbIdsDatabase.Instance.ResolveProduct(v, p)
                : null;
            productName ??= WmiQueryHelper.GetString(row, "Name")
                ?? WmiQueryHelper.GetString(row, "Description");

            // Read topology (parent hub instance + port number) and ask
            // the hub IOCTL layer for authoritative speed.
            var topo = DeviceTopologyReader.Read(instanceId);
            var speed = topo is { ParentInstanceId: { } parent, PortAddress: { } addr }
                ? hubReader.GetNegotiatedSpeed(parent, addr)
                : UsbDataRate.Unknown;

            yield return new ConnectedDevice
            {
                InstanceId = instanceId,
                FriendlyName = productName,
                Manufacturer = vendorName,
                VendorId = vid,
                ProductId = pid,
                NegotiatedRate = speed,
                DeviceClass = WmiQueryHelper.GetString(row, "PNPClass"),
            };

            await Task.Yield();
        }
    }

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
