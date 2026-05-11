using System.Runtime.Versioning;
using UsbScope.Core.Providers;
using UsbScope.Providers.Windows.Wmi;

namespace UsbScope.Providers.Windows.Providers;

/// Surfaces fields from Dell's DCIM WMI provider when present.
/// Detection: the root\dcim\sysman namespace exists.
///
/// Phase 1 only reads battery / power-supply info that doesn't already
/// come from generic WMI. We deliberately avoid impersonating Dell
/// Command-line or Dell Command | Configure — we just read what
/// Dell publishes to local WMI for free.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class DellVendorProvider : IVendorProvider
{
    private const string Namespace = @"\\.\root\dcim\sysman";

    public string VendorName => "Dell";

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default)
        => ValueTask.FromResult(WmiQueryHelper.NamespaceExists(Namespace));

    public ValueTask<IReadOnlyDictionary<string, string>> EnrichAsync(
        string portId,
        CancellationToken ct = default)
    {
        var props = new Dictionary<string, string>();

        // DCIM_BatteryChargeSetting / DCIM_PowerSupply expose AC wattage,
        // battery state, etc. These are system-wide, not per-port. We
        // still surface them so the bottleneck inference can use them
        // ("PC sees a 65W adapter, but you're plugged into a 30W brick").
        foreach (var ps in WmiQueryHelper.Query(Namespace, "SELECT * FROM DCIM_PowerSupply"))
        {
            ct.ThrowIfCancellationRequested();
            var watts = WmiQueryHelper.GetString(ps, "Wattage");
            if (!string.IsNullOrEmpty(watts))
                props["vendor.dell.AcAdapterWatts"] = watts;

            var type = WmiQueryHelper.GetString(ps, "Description");
            if (!string.IsNullOrEmpty(type))
                props["vendor.dell.PowerSupplyDesc"] = type;
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, string>>(props);
    }
}
