using System.Text.Json;
using UsbScope.Core.Billboard;
using UsbScope.Core.Models;
using UsbScope.Providers.Windows;

namespace UsbScope.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var json = args.Contains("--json");
        var help = args.Contains("--help") || args.Contains("-h");

        if (help)
        {
            Console.WriteLine("usbscope — USB port diagnostics for Windows");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  usbscope          Plain-text summary");
            Console.WriteLine("  usbscope --json   Machine-readable snapshot");
            return 0;
        }

        var service = new SnapshotService();
        var snapshot = await service.CaptureAsync();

        if (json)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            };
            Console.WriteLine(JsonSerializer.Serialize(snapshot, options));
            return 0;
        }

        Console.WriteLine($"usbscope snapshot — host {snapshot.HostMachine}");
        Console.WriteLine($"Captured at {snapshot.CapturedAt:O}");

        Console.WriteLine();
        Console.WriteLine($"Ports ({snapshot.Ports.Count} physical, from firmware):");
        if (snapshot.Ports.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var port in snapshot.Ports)
                Console.WriteLine($"  - [{PhysicalTag(port.PhysicalType)}] {port.Label ?? port.PortId}");
        }

        Console.WriteLine();
        Console.WriteLine($"Devices ({snapshot.Devices.Count} on the bus):");
        if (snapshot.Devices.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var d in snapshot.Devices)
            {
                var vidPid = d.VendorId is { } v && d.ProductId is { } p
                    ? $" [{v:X4}:{p:X4}]"
                    : "";
                Console.WriteLine($"  - {d.FriendlyName ?? d.InstanceId}{vidPid}");
                if (!string.IsNullOrEmpty(d.Manufacturer))
                    Console.WriteLine($"      Vendor : {d.Manufacturer}");
                if (d.NegotiatedRate != UsbDataRate.Unknown)
                    Console.WriteLine($"      Speed  : {SpeedLabel(d.NegotiatedRate)}");
                if (d.Billboard is { AlternateModes.Count: > 0 } bb)
                {
                    var modes = string.Join(", ",
                        bb.AlternateModes.Select(a => AltModeSvidRegistry.Describe(a.Svid)));
                    Console.WriteLine($"      AltMode: {modes}");
                }
            }
        }

        foreach (var diag in snapshot.Diagnostics)
            Console.Error.WriteLine($"diag: {diag}");

        return 0;
    }

    private static string PhysicalTag(ConnectorPhysicalType type) => type switch
    {
        ConnectorPhysicalType.UsbTypeC => "USB-C",
        ConnectorPhysicalType.UsbTypeA => "USB-A",
        ConnectorPhysicalType.UsbTypeB => "USB-B",
        ConnectorPhysicalType.UsbMiniB => "Mini-B",
        ConnectorPhysicalType.UsbMicroB => "Micro-B",
        ConnectorPhysicalType.Internal => "internal",
        _ => "USB",
    };

    private static string SpeedLabel(UsbDataRate rate) => rate switch
    {
        UsbDataRate.LowSpeed1_5Mbps => "USB 1.0 Low-Speed (1.5 Mbps)",
        UsbDataRate.FullSpeed12Mbps => "USB 1.1 Full-Speed (12 Mbps)",
        UsbDataRate.HighSpeed480Mbps => "USB 2.0 High-Speed (480 Mbps)",
        UsbDataRate.SuperSpeed5Gbps => "USB 3.0 SuperSpeed (5 Gbps)",
        UsbDataRate.SuperSpeedPlus10Gbps => "USB 3.1+ SuperSpeed+ (10–20 Gbps)",
        UsbDataRate.SuperSpeedPlus20Gbps => "USB 3.2 Gen 2x2 (20 Gbps)",
        UsbDataRate.Usb4Gen2x2_20Gbps => "USB4 Gen 2x2 (20 Gbps)",
        UsbDataRate.Usb4Gen3x2_40Gbps => "USB4 Gen 3x2 (40 Gbps)",
        UsbDataRate.Usb4Gen4_80Gbps => "USB4 Gen 4 (80 Gbps)",
        _ => "unknown",
    };
}
