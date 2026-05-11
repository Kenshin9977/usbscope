using System.Runtime.CompilerServices;
using UsbScope.Core.Models;
using UsbScope.Core.Providers;
using UsbScope.Providers.Windows;

namespace UsbScope.Providers.Windows.Tests;

public class SnapshotServiceTests
{
    [Fact]
    public async Task Skips_Unavailable_Providers()
    {
        var port = new FakePortProvider("present", available: true, ports: [MakePort("p1")]);
        var absent = new FakePortProvider("absent", available: false, ports: [MakePort("p-missing")]);

        var service = new SnapshotService([port, absent], [], []);
        var snapshot = await service.CaptureAsync();

        Assert.Single(snapshot.Ports);
        Assert.Equal("p1", snapshot.Ports[0].PortId);
        Assert.Contains("present", snapshot.ProviderNames);
        Assert.DoesNotContain("absent", snapshot.ProviderNames);
    }

    [Fact]
    public async Task Records_Diagnostic_When_Port_Provider_Throws()
    {
        var faulty = new FakePortProvider("faulty", available: true, throwOnEnumerate: true);

        var service = new SnapshotService([faulty], [], []);
        var snapshot = await service.CaptureAsync();

        Assert.Empty(snapshot.Ports);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("'faulty'") && d.Contains("threw"));
    }

    [Fact]
    public async Task Merges_Power_Info_Onto_Each_Port()
    {
        var port = new FakePortProvider("p", available: true, ports: [MakePort("usb-c-1")]);
        var power = new FakePowerProvider("battery", available: true, reading: new PowerInfo
        {
            Role = PowerRole.Sink,
            ObservedChargeRateW = 27.4,
        });

        var service = new SnapshotService([port], [power], []);
        var snapshot = await service.CaptureAsync();

        Assert.Single(snapshot.Ports);
        Assert.Equal(27.4, snapshot.Ports[0].Power.ObservedChargeRateW);
        Assert.Equal(PowerRole.Sink, snapshot.Ports[0].Power.Role);
    }

    [Fact]
    public async Task Merges_Vendor_Properties_Into_RawProperties()
    {
        var port = new FakePortProvider("p", available: true, ports: [MakePort("usb-c-1")]);
        var vendor = new FakeVendorProvider("Dell", available: true, fields: new Dictionary<string, string>
        {
            ["vendor.dell.AcAdapterWatts"] = "130",
        });

        var service = new SnapshotService([port], [], [vendor]);
        var snapshot = await service.CaptureAsync();

        Assert.Equal("130", snapshot.Ports[0].RawProperties["vendor.dell.AcAdapterWatts"]);
    }

    [Fact]
    public async Task Diagnostic_When_No_Ports_But_Providers_Were_Active()
    {
        var port = new FakePortProvider("empty", available: true, ports: []);

        var service = new SnapshotService([port], [], []);
        var snapshot = await service.CaptureAsync();

        Assert.Empty(snapshot.Ports);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("No USB ports detected"));
    }

    [Fact]
    public async Task Vendor_Throwing_On_Enrich_Does_Not_Lose_Port()
    {
        var port = new FakePortProvider("p", available: true, ports: [MakePort("usb-c-1")]);
        var vendor = new FakeVendorProvider("Broken", available: true, throwOnEnrich: true);

        var service = new SnapshotService([port], [], [vendor]);
        var snapshot = await service.CaptureAsync();

        Assert.Single(snapshot.Ports);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("'Broken'") && d.Contains("threw"));
    }

    private static UsbPort MakePort(string id) => new()
    {
        PortId = id,
        Label = id,
    };

    private sealed class FakePortProvider(
        string name,
        bool available,
        IReadOnlyList<UsbPort>? ports = null,
        bool throwOnEnumerate = false) : IPortProvider
    {
        public string Name => name;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default) => ValueTask.FromResult(available);

        public async IAsyncEnumerable<UsbPort> EnumeratePortsAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            if (throwOnEnumerate) throw new InvalidOperationException("boom");
            foreach (var p in ports ?? [])
            {
                ct.ThrowIfCancellationRequested();
                yield return p;
                await Task.Yield();
            }
        }
    }

    private sealed class FakePowerProvider(string name, bool available, PowerInfo reading) : IPowerProvider
    {
        public string Name => name;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default) => ValueTask.FromResult(available);
        public ValueTask<PowerInfo> ReadAsync(string portId, CancellationToken ct = default) => ValueTask.FromResult(reading);
    }

    private sealed class FakeVendorProvider(
        string vendor,
        bool available,
        IReadOnlyDictionary<string, string>? fields = null,
        bool throwOnEnrich = false) : IVendorProvider
    {
        public string VendorName => vendor;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct = default) => ValueTask.FromResult(available);
        public ValueTask<IReadOnlyDictionary<string, string>> EnrichAsync(string portId, CancellationToken ct = default)
        {
            if (throwOnEnrich) throw new InvalidOperationException("boom");
            return ValueTask.FromResult(fields ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>());
        }
    }
}
