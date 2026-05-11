using System.Runtime.Versioning;
using UsbScope.Core.Models;
using UsbScope.Core.Providers;
using UsbScope.Providers.Ucsi;
using UsbScope.Providers.Windows.Providers;
using UsbScope.Providers.Windows.UsbDevices;

namespace UsbScope.Providers.Windows;

/// Aggregates IPortProvider / IPowerProvider / IVendorProvider results
/// into a single PortSnapshot. Skips unavailable providers, records
/// per-provider failures as diagnostics rather than letting them break
/// the read. The UI and the CLI both go through this.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class SnapshotService : ISnapshotService
{
    private readonly IReadOnlyList<IPortProvider> _portProviders;
    private readonly IReadOnlyList<IPowerProvider> _powerProviders;
    private readonly IReadOnlyList<IVendorProvider> _vendorProviders;

    /// Default constructor wires the standard Windows providers.
    /// Tests can construct directly with fakes. Port providers run in
    /// declaration order; the aggregator dedupes by PortId so the
    /// *first* provider to surface a given port wins.
    ///
    /// Order rationale:
    ///   1. SMBIOS  — every PC, physical port list, no driver needed.
    ///   2. UCSI    — Phase 2 driver. Adds PD negotiation data per UCSI
    ///                connector. Probed first via PING IOCTL; if the
    ///                driver isn't installed, the provider is skipped
    ///                cleanly.
    ///   3. WmiUcm  — legacy fallback for older UCM stacks where the
    ///                driver isn't yet attached.
    public SnapshotService()
        : this(
            [new SmbiosPortProvider(), new UcsiPortProvider(), new WmiPortProvider()],
            [new WmiBatteryPowerProvider()],
            [new DellVendorProvider(), new LenovoVendorProvider()])
    {
    }

    public SnapshotService(
        IReadOnlyList<IPortProvider> portProviders,
        IReadOnlyList<IPowerProvider> powerProviders,
        IReadOnlyList<IVendorProvider> vendorProviders)
    {
        _portProviders = portProviders;
        _powerProviders = powerProviders;
        _vendorProviders = vendorProviders;
    }

    public async ValueTask<PortSnapshot> CaptureAsync(CancellationToken ct = default)
    {
        var diagnostics = new List<string>();
        var providerNames = new List<string>();
        var ports = new List<UsbPort>();

        var activePortProviders = await FilterAvailableAsync(_portProviders, p => p.Name, p => p.IsAvailableAsync(ct), diagnostics, ct);
        var activePowerProviders = await FilterAvailableAsync(_powerProviders, p => p.Name, p => p.IsAvailableAsync(ct), diagnostics, ct);
        var activeVendorProviders = await FilterAvailableAsync(_vendorProviders, p => p.VendorName, p => p.IsAvailableAsync(ct), diagnostics, ct);

        providerNames.AddRange(activePortProviders.Select(p => p.Name));
        providerNames.AddRange(activePowerProviders.Select(p => p.Name));
        providerNames.AddRange(activeVendorProviders.Select(p => "vendor:" + p.VendorName));

        var seenPortIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in activePortProviders)
        {
            try
            {
                await foreach (var port in provider.EnumeratePortsAsync(ct).WithCancellation(ct))
                {
                    if (!seenPortIds.Add(port.PortId))
                    {
                        diagnostics.Add($"port provider '{provider.Name}' reported a duplicate PortId '{port.PortId}'; first wins.");
                        continue;
                    }
                    ports.Add(port);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                diagnostics.Add($"port provider '{provider.Name}' threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Enrich each port with power + vendor data. We swallow per-provider
        // failures so one broken WMI namespace doesn't kill the whole read.
        for (var i = 0; i < ports.Count; i++)
        {
            ports[i] = await EnrichAsync(ports[i], activePowerProviders, activeVendorProviders, diagnostics, ct);
        }

        if (ports.Count == 0 && activePortProviders.Count > 0)
            diagnostics.Add("No USB ports detected. Firmware reports no USB connectors. This is unusual — check that SMBIOS Type 8 records are present (`wmic path Win32_PortConnector get`).");

        var devices = new List<ConnectedDevice>();
        using (var hubReader = new UsbHubReader())
        {
            try
            {
                await foreach (var d in UsbDeviceEnumerator.EnumerateAsync(hubReader, ct).WithCancellation(ct))
                    devices.Add(d);
                providerNames.Add("usb:device-tree");
                providerNames.Add("usb:hub-ioctl");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                diagnostics.Add($"USB device enumerator threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new PortSnapshot
        {
            CapturedAt = DateTimeOffset.Now,
            HostMachine = Environment.MachineName,
            Ports = ports,
            Devices = devices,
            ProviderNames = providerNames,
            Diagnostics = diagnostics,
        };
    }

    private static async ValueTask<UsbPort> EnrichAsync(
        UsbPort port,
        IReadOnlyList<IPowerProvider> powerProviders,
        IReadOnlyList<IVendorProvider> vendorProviders,
        List<string> diagnostics,
        CancellationToken ct)
    {
        var power = port.Power;
        foreach (var pp in powerProviders)
        {
            try
            {
                var info = await pp.ReadAsync(port.PortId, ct);
                power = MergePower(power, info);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                diagnostics.Add($"power provider '{pp.Name}' threw on port '{port.PortId}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        var props = new Dictionary<string, string>(port.RawProperties);
        foreach (var vp in vendorProviders)
        {
            try
            {
                var fields = await vp.EnrichAsync(port.PortId, ct);
                foreach (var (k, v) in fields)
                    props[k] = v;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                diagnostics.Add($"vendor provider '{vp.VendorName}' threw on port '{port.PortId}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        return port with { Power = power, RawProperties = props };
    }

    private static PowerInfo MergePower(PowerInfo current, PowerInfo incoming) => current with
    {
        Role = incoming.Role != PowerRole.Unknown ? incoming.Role : current.Role,
        NegotiatedVoltageMv = incoming.NegotiatedVoltageMv ?? current.NegotiatedVoltageMv,
        NegotiatedCurrentMa = incoming.NegotiatedCurrentMa ?? current.NegotiatedCurrentMa,
        ObservedChargeRateW = incoming.ObservedChargeRateW ?? current.ObservedChargeRateW,
        PartnerAdvertisedMaxW = incoming.PartnerAdvertisedMaxW ?? current.PartnerAdvertisedMaxW,
    };

    private static async ValueTask<List<T>> FilterAvailableAsync<T>(
        IReadOnlyList<T> providers,
        Func<T, string> nameOf,
        Func<T, ValueTask<bool>> probe,
        List<string> diagnostics,
        CancellationToken ct)
    {
        var active = new List<T>(providers.Count);
        foreach (var p in providers)
        {
            try
            {
                if (await probe(p))
                    active.Add(p);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                diagnostics.Add($"provider '{nameOf(p)}' threw on probe: {ex.GetType().Name}: {ex.Message}");
            }
            ct.ThrowIfCancellationRequested();
        }
        return active;
    }
}
