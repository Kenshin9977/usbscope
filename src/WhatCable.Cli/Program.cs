using System.Text.Json;
using WhatCable.Providers.Windows;

namespace WhatCable.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var json = args.Contains("--json");
        var help = args.Contains("--help") || args.Contains("-h");

        if (help)
        {
            Console.WriteLine("whatcable — USB-C port diagnostics for Windows");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  whatcable          Plain-text summary");
            Console.WriteLine("  whatcable --json   Machine-readable snapshot");
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

        Console.WriteLine($"WhatCable snapshot — host {snapshot.HostMachine}");
        Console.WriteLine($"Captured at {snapshot.CapturedAt:O}");
        Console.WriteLine();
        if (snapshot.Ports.Count == 0)
        {
            Console.WriteLine("No USB-C ports detected yet.");
        }
        else
        {
            foreach (var port in snapshot.Ports)
            {
                Console.WriteLine($"- {port.Label ?? port.PortId}");
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
