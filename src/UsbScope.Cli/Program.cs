using System.Text.Json;
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
        if (snapshot.Ports.Count == 0)
        {
            Console.WriteLine("No USB ports detected.");
        }
        else
        {
            foreach (var port in snapshot.Ports)
            {
                var typeTag = port.PhysicalType switch
                {
                    UsbScope.Core.Models.ConnectorPhysicalType.UsbTypeC => "USB-C",
                    UsbScope.Core.Models.ConnectorPhysicalType.UsbTypeA => "USB-A",
                    UsbScope.Core.Models.ConnectorPhysicalType.UsbTypeB => "USB-B",
                    UsbScope.Core.Models.ConnectorPhysicalType.UsbMiniB => "Mini-B",
                    UsbScope.Core.Models.ConnectorPhysicalType.UsbMicroB => "Micro-B",
                    UsbScope.Core.Models.ConnectorPhysicalType.Internal => "internal",
                    _ => "USB",
                };
                Console.WriteLine($"- [{typeTag}] {port.Label ?? port.PortId}");
                if (port.Device is { } d)
                    Console.WriteLine($"    Device: {d.FriendlyName} ({d.NegotiatedRate})");
                if (port.Power.ObservedChargeRateW is double w)
                    Console.WriteLine($"    Power : {w:F1} W observed");
            }
        }

        foreach (var d in snapshot.Diagnostics)
            Console.Error.WriteLine($"diag: {d}");

        return 0;
    }
}
