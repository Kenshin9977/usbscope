using System.Runtime.Versioning;
using UsbScope.Core.Models;
using UsbScope.Core.Providers;
using UsbScope.Providers.Windows.Wmi;

namespace UsbScope.Providers.Windows.Providers;

/// Phase 1 power: observed instantaneous charge rate via the
/// root\wmi BatteryStatus class. ChargeRate is signed mW (positive
/// while charging, zero or negative when discharging). We surface
/// the host-wide rate because without UCSI we cannot attribute charge
/// to a specific connector; the UI shows it on all ports and the
/// aggregator's diagnostics flag this so engineer mode can explain it.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class WmiBatteryPowerProvider : IPowerProvider
{
    public string Name => "wmi:battery";

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default)
        => ValueTask.FromResult(WmiQueryHelper.NamespaceExists(@"\\.\root\wmi"));

    public ValueTask<PowerInfo> ReadAsync(string portId, CancellationToken ct = default)
    {
        var hostRate = ReadObservedRateWatts();
        return ValueTask.FromResult(new PowerInfo
        {
            Role = PowerRole.Sink,
            ObservedChargeRateW = hostRate,
        });
    }

    /// Returns null when no battery is reachable (desktop, server, broken WMI).
    private static double? ReadObservedRateWatts()
    {
        int? chargeRateMw = null;
        foreach (var status in WmiQueryHelper.Query(@"\\.\root\wmi", "SELECT ChargeRate FROM BatteryStatus"))
        {
            var v = WmiQueryHelper.Get<int>(status, "ChargeRate");
            if (v.HasValue)
            {
                chargeRateMw = v.Value;
                break;
            }
        }
        if (chargeRateMw is null) return null;
        return chargeRateMw.Value / 1000.0;
    }
}
