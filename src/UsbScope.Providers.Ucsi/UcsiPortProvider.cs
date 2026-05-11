using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using UsbScope.Core.Models;
using UsbScope.Core.Providers;

namespace UsbScope.Providers.Ucsi;

/// IPortProvider backed by the UsbScope kernel driver. When the driver
/// is installed and reports UCSI capability, this surfaces one
/// `UsbPort` per UCSI connector with real PD negotiation data
/// (PowerRole, NegotiatedVoltageMv/CurrentMa, partner type) — the
/// stuff Phase 1 SMBIOS alone can't give us.
///
/// When the driver is *not* installed, `IsAvailableAsync` returns
/// false and the aggregator records a single diagnostic. That keeps
/// every machine, with or without the driver, returning a clean
/// snapshot — only the level of detail differs.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class UcsiPortProvider : IPortProvider
{
    public string Name => "ucsi:driver";

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        using var client = new UcsiDriverClient();
        if (!client.IsConnected)
            return ValueTask.FromResult(false);

        if (!client.TryPing(out var ping))
            return ValueTask.FromResult(false);

        var major = UcsiIoctl.MajorVersion(ping.Version);
        return ValueTask.FromResult(major == UcsiIoctl.USBSCOPE_DRIVER_MAJOR);
    }

    public async IAsyncEnumerable<UsbPort> EnumeratePortsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = new UcsiDriverClient();
        if (!client.IsConnected) yield break;
        if (!client.TryGetUcsiState(out var state)) yield break;

        for (int i = 0; i < state.NumberOfConnectors && i < 8; i++)
        {
            ct.ThrowIfCancellationRequested();
            var c = state[i];

            yield return new UsbPort
            {
                PortId = $"ucsi:connector-{c.ConnectorIndex}",
                Label = $"UCSI connector {c.ConnectorIndex}",
                PhysicalType = ConnectorPhysicalType.UsbTypeC,
                Role = ConnectorRole.DualRoleData,
                Power = new PowerInfo
                {
                    Role = c.PowerDirection == 1 ? PowerRole.Source : PowerRole.Sink,
                    NegotiatedVoltageMv = c.NegotiatedVoltageMv > 0 ? (int)c.NegotiatedVoltageMv : null,
                    NegotiatedCurrentMa = c.NegotiatedCurrentMa > 0 ? (int)c.NegotiatedCurrentMa : null,
                },
                RawProperties = new Dictionary<string, string>
                {
                    ["ucsi.PowerOperationMode"] = DescribePowerOperationMode(c.PowerOperationMode),
                    ["ucsi.PartnerType"]        = DescribePartnerType(c.PartnerType),
                    ["ucsi.ConnectStatus"]      = c.ConnectStatus == 1 ? "connected" : "empty",
                    ["ucsi.Version"]            = $"{state.UcsiVersionMajor}.{state.UcsiVersionMinor}",
                },
            };

            await Task.Yield();
        }
    }

    private static string DescribePowerOperationMode(byte mode) => mode switch
    {
        0 => "unknown",
        1 => "USB Default (vSafe5V, 500/900 mA)",
        2 => "BC (Battery Charging 1.2)",
        3 => "USB-PD",
        4 => "Type-C 1.5A",
        5 => "Type-C 3.0A",
        _ => $"reserved ({mode})",
    };

    private static string DescribePartnerType(byte type) => type switch
    {
        0 => "unknown",
        1 => "DFP (downstream-facing partner)",
        2 => "UFP (upstream-facing partner)",
        3 => "powered cable with UFP attached",
        4 => "powered cable, no UFP attached",
        5 => "debug accessory",
        6 => "audio accessory",
        _ => $"reserved ({type})",
    };
}
