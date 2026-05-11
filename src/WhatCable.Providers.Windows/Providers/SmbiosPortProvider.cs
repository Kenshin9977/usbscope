using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using WhatCable.Core.Models;
using WhatCable.Core.Providers;
using WhatCable.Providers.Windows.Wmi;

namespace WhatCable.Providers.Windows.Providers;

/// Vendor-agnostic port enumeration via SMBIOS Type 8 (Port Connector
/// Information), surfaced by Windows as the Win32_PortConnector WMI
/// class. Reports every USB port the OEM declared in firmware,
/// regardless of which stack (UCM, vendor PD, none) is wired up
/// underneath. Identifies the physical receptacle type from the
/// designator so USB-C-specific features stay gated to USB-C rows.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class SmbiosPortProvider : IPortProvider
{
    public string Name => "smbios:portconnector";

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default)
        => ValueTask.FromResult(WmiQueryHelper.NamespaceExists(@"\\.\root\cimv2"));

    public async IAsyncEnumerable<UsbPort> EnumeratePortsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        const string wql = """
            SELECT Tag, ExternalReferenceDesignator, InternalReferenceDesignator, PortType
            FROM Win32_PortConnector
            """;

        foreach (var row in WmiQueryHelper.Query(@"\\.\root\cimv2", wql))
        {
            ct.ThrowIfCancellationRequested();

            var portType = WmiQueryHelper.Get<ushort>(row, "PortType");
            var external = WmiQueryHelper.GetString(row, "ExternalReferenceDesignator") ?? "";

            if (!IsUsbPort(portType, external))
                continue;

            var @internal = WmiQueryHelper.GetString(row, "InternalReferenceDesignator") ?? "";
            var tag = WmiQueryHelper.GetString(row, "Tag") ?? "";
            var physical = ClassifyConnector(external, @internal);

            yield return new UsbPort
            {
                PortId = string.IsNullOrEmpty(@internal) ? tag : $"smbios:{@internal}",
                Label = external,
                PhysicalType = physical,
                Role = physical == ConnectorPhysicalType.UsbTypeC
                    ? ConnectorRole.DualRoleData
                    : ConnectorRole.DownstreamFacingPort,
                RawProperties = new Dictionary<string, string>
                {
                    ["smbios.Tag"] = tag,
                    ["smbios.ExternalDesignator"] = external,
                    ["smbios.InternalDesignator"] = @internal,
                    ["smbios.PortType"] = portType?.ToString() ?? "",
                },
            };

            await Task.Yield();
        }
    }

    /// Win32_PortConnector.PortType: 16 = USB, 17 = IEEE 1394 (FireWire),
    /// others we don't care about. Some firmwares leave PortType=0 for
    /// USB-C; we fall back to the designator string in that case.
    private static bool IsUsbPort(ushort? portType, string designator)
    {
        if (portType == 16) return true;
        if (string.IsNullOrEmpty(designator)) return false;
        var d = designator.ToUpperInvariant();
        return d.Contains("USB") || d.Contains("TYPE-C") || d.Contains("USB4");
    }

    /// Best-effort classification from the printable designator + internal
    /// label. Firmwares are wildly inconsistent ("USB-C", "Type-C", "USB
    /// Type C", "USB4", "USB 3.2 Type-C"…) so we accept all common forms.
    private static ConnectorPhysicalType ClassifyConnector(string external, string @internal)
    {
        var combined = (external + " " + @internal).ToUpperInvariant();

        if (combined.Contains("USB-C") || combined.Contains("USB TYPE-C")
            || combined.Contains("USB TYPE C") || combined.Contains("TYPE-C")
            || combined.Contains("USB4") || combined.Contains("USB 4")
            || combined.Contains("THUNDERBOLT") || combined.Contains("TBT"))
            return ConnectorPhysicalType.UsbTypeC;

        if (combined.Contains("MICRO-B") || combined.Contains("MICROB") || combined.Contains("MICRO USB"))
            return ConnectorPhysicalType.UsbMicroB;

        if (combined.Contains("MINI-B") || combined.Contains("MINIB") || combined.Contains("MINI USB"))
            return ConnectorPhysicalType.UsbMiniB;

        if (combined.Contains("INTERNAL") || combined.StartsWith("J") && !combined.Contains("USB"))
            return ConnectorPhysicalType.Internal;

        if (combined.Contains("USB-B") || combined.Contains("TYPE-B") || combined.Contains("TYPE B"))
            return ConnectorPhysicalType.UsbTypeB;

        // Default: most "USB", "USB 2.0", "USB 3.0", "USB 3.1" external
        // designators on consumer hardware refer to Type-A receptacles.
        return ConnectorPhysicalType.UsbTypeA;
    }
}
