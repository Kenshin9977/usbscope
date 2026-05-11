using System.Runtime.Versioning;
using WhatCable.Core.Providers;
using WhatCable.Providers.Windows.Wmi;

namespace WhatCable.Providers.Windows.Providers;

/// Surfaces fields from Lenovo's WMI providers when present.
/// Detection: a Lenovo_* class is reachable in root\WMI. Lenovo Vantage
/// uses these same providers; we just read public properties.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class LenovoVendorProvider : IVendorProvider
{
    private const string Namespace = @"\\.\root\WMI";

    public string VendorName => "Lenovo";

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!WmiQueryHelper.NamespaceExists(Namespace))
            return ValueTask.FromResult(false);

        // Cheap probe: try Lenovo_BIOSElement which most ThinkPad/Yoga
        // machines expose. If absent, this isn't a Lenovo box.
        var present = WmiQueryHelper.Query(Namespace, "SELECT InstanceName FROM Lenovo_BIOSElement").Any();
        return ValueTask.FromResult(present);
    }

    public ValueTask<IReadOnlyDictionary<string, string>> EnrichAsync(
        string portId,
        CancellationToken ct = default)
    {
        var props = new Dictionary<string, string>();

        // Lenovo_PowerSettings exposes per-system power profile data.
        // Lenovo_BatteryControl on supported ThinkPads exposes charge
        // thresholds. We surface a small whitelist; engineer mode in
        // the UI can dump the full set on demand.
        foreach (var ps in WmiQueryHelper.Query(Namespace, "SELECT * FROM Lenovo_PowerSettings"))
        {
            ct.ThrowIfCancellationRequested();
            var current = WmiQueryHelper.GetString(ps, "CurrentSetting");
            if (!string.IsNullOrEmpty(current))
                props["vendor.lenovo.PowerProfile"] = current;
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, string>>(props);
    }
}
