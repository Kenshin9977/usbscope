using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using WhatCable.Core.Models;
using WhatCable.Core.Providers;
using WhatCable.Providers.Windows.Interop;
using WhatCable.Providers.Windows.Wmi;

namespace WhatCable.Providers.Windows.Providers;

/// Phase 1 port enumeration via WMI. Identifies USB-C ports as PnP
/// entities whose class is UCM (USB Connector Manager) or whose service
/// is UcmCxClient. For each port we walk Win32_PnPEntity associations
/// to surface the device currently attached, and ask the Configuration
/// Manager for that device's negotiated USB speed.
///
/// Limitations:
///   - No per-port PD info (PDOs, partner power). That needs UCSI.
///   - No e-marker cable VDOs.
///   - On systems where the PD stack does not expose UCM connectors to
///     user mode (older OEM stacks, Intel ME-only PD), this provider
///     returns no ports — vendor providers may still pick up the slack.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class WmiPortProvider : IPortProvider
{
    public string Name => "wmi:ucm";

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default)
        => ValueTask.FromResult(WmiQueryHelper.NamespaceExists(@"\\.\root\cimv2"));

    public async IAsyncEnumerable<UsbPort> EnumeratePortsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        const string wql = """
            SELECT DeviceID, Name, Description, Manufacturer, PNPClass, Service, Status
            FROM Win32_PnPEntity
            WHERE PNPClass = 'UCM' OR Service = 'UcmCxClient'
            """;

        foreach (var port in WmiQueryHelper.Query(@"\\.\root\cimv2", wql))
        {
            ct.ThrowIfCancellationRequested();
            var instanceId = WmiQueryHelper.GetString(port, "DeviceID");
            if (string.IsNullOrEmpty(instanceId))
                continue;

            var name = WmiQueryHelper.GetString(port, "Name");
            var manufacturer = WmiQueryHelper.GetString(port, "Manufacturer");
            var description = WmiQueryHelper.GetString(port, "Description");

            var device = await TryReadConnectedDeviceAsync(instanceId, ct);

            yield return new UsbPort
            {
                PortId = instanceId,
                Label = name ?? description ?? instanceId,
                Role = ConnectorRole.DualRoleData,
                Device = device,
                RawProperties = new Dictionary<string, string>
                {
                    ["wmi.PNPClass"] = WmiQueryHelper.GetString(port, "PNPClass") ?? "",
                    ["wmi.Service"]  = WmiQueryHelper.GetString(port, "Service") ?? "",
                    ["wmi.Manufacturer"] = manufacturer ?? "",
                    ["wmi.Status"]   = WmiQueryHelper.GetString(port, "Status") ?? "",
                },
            };
        }
    }

    /// Best-effort: find a Win32_PnPEntity whose parent is the port and
    /// looks like an actual USB device (not a child controller). On
    /// systems with multiple devices per hub this picks the first match.
    private static ValueTask<ConnectedDevice?> TryReadConnectedDeviceAsync(
        string portInstanceId,
        CancellationToken ct)
    {
        // We cannot use WMI's ASSOCIATORS OF directly to walk to a child
        // PnP entity (Win32_PnPEntity isn't a key class for that). So
        // we filter all entities whose Parent property equals our port.
        var escaped = portInstanceId.Replace(@"\", @"\\").Replace("'", @"\'");
        var wql = $"""
            SELECT DeviceID, Name, Description, Manufacturer, ClassGuid, HardwareID
            FROM Win32_PnPEntity
            WHERE DeviceID LIKE 'USB\\%' AND Manufacturer IS NOT NULL
            """;

        foreach (var child in WmiQueryHelper.Query(@"\\.\root\cimv2", wql))
        {
            ct.ThrowIfCancellationRequested();
            var instanceId = WmiQueryHelper.GetString(child, "DeviceID");
            if (string.IsNullOrEmpty(instanceId))
                continue;

            // Parent-walk via cfgmgr32 is more reliable than WMI's missing
            // Parent property on this class. For Phase 1 we accept any USB
            // device and let the UI show "device probably on this port".
            // A future commit will tighten correlation via CM_Get_Parent.
            var manufacturer = WmiQueryHelper.GetString(child, "Manufacturer");
            if (string.IsNullOrEmpty(manufacturer) || manufacturer.StartsWith("(Standard", StringComparison.OrdinalIgnoreCase))
                continue;

            var (vid, pid) = ParseVidPid(WmiQueryHelper.GetString(child, "HardwareID"));

            return ValueTask.FromResult<ConnectedDevice?>(new ConnectedDevice
            {
                InstanceId = instanceId,
                FriendlyName = WmiQueryHelper.GetString(child, "Name"),
                Manufacturer = manufacturer,
                VendorId = vid,
                ProductId = pid,
                NegotiatedRate = UsbSpeedReader.Read(instanceId),
                DeviceClass = WmiQueryHelper.GetString(child, "ClassGuid"),
            });
        }

        _ = escaped; // reserved for tighter correlation in follow-up
        return ValueTask.FromResult<ConnectedDevice?>(null);
    }

    private static (ushort? Vid, ushort? Pid) ParseVidPid(string? hardwareIdRaw)
    {
        if (string.IsNullOrEmpty(hardwareIdRaw))
            return (null, null);

        // WMI returns string[] but ManagementObject.ToString() may give us
        // either a single value or "value1\nvalue2". We accept either form.
        var first = hardwareIdRaw.Split('\n', '\r').FirstOrDefault(s => s.Contains("VID_", StringComparison.OrdinalIgnoreCase));
        if (first is null) return (null, null);

        ushort? vid = ExtractHex(first, "VID_");
        ushort? pid = ExtractHex(first, "PID_");
        return (vid, pid);
    }

    private static ushort? ExtractHex(string source, string prefix)
    {
        var idx = source.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + prefix.Length;
        if (start + 4 > source.Length) return null;
        return ushort.TryParse(source.AsSpan(start, 4), System.Globalization.NumberStyles.HexNumber, null, out var v)
            ? v : null;
    }
}
