using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace WhatCable.Core.UsbIds;

/// Vendor-agnostic USB vendor/product name resolution backed by the
/// embedded `usb.ids` database. Replaces fragile WMI Manufacturer
/// strings (which vary wildly across drivers).
///
/// The database is parsed once on first access and held in memory
/// (about 200 KB resident). Concurrent lookups are safe — the parse
/// runs under a Lazy with thread-safety enabled.
public sealed class UsbIdsDatabase
{
    public static UsbIdsDatabase Instance { get; } = new();

    private readonly Lazy<Dictionary<ushort, VendorEntry>> _byVendor;

    private UsbIdsDatabase()
    {
        _byVendor = new Lazy<Dictionary<ushort, VendorEntry>>(Load, isThreadSafe: true);
    }

    public string? ResolveVendor(ushort vendorId)
        => _byVendor.Value.TryGetValue(vendorId, out var v) ? v.Name : null;

    public string? ResolveProduct(ushort vendorId, ushort productId)
    {
        if (!_byVendor.Value.TryGetValue(vendorId, out var v)) return null;
        return v.Products.TryGetValue(productId, out var product) ? product : null;
    }

    private static Dictionary<ushort, VendorEntry> Load()
    {
        var assembly = typeof(UsbIdsDatabase).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("usb.ids.gz", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return [];

        using var compressed = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("usb.ids.gz resource is registered but its stream is null.");
        using var gz = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);

        var result = new Dictionary<ushort, VendorEntry>(capacity: 4096);
        VendorEntry? currentVendor = null;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;

            // Subclass / interface lines for classes section begin with
            // class blocks ('C 09', etc.) — we only care about the
            // vendor section, which precedes them.
            if (line[0] is 'C' or 'A' or 'H' or 'L' or 'P' or 'R' or 'V' or 'B' or 'T')
                break;

            if (line[0] == '\t')
            {
                if (currentVendor is null) continue;
                if (line.Length >= 2 && line[1] == '\t') continue; // skip sub-product interface lines

                var trimmed = line.AsSpan(1);
                var (id, name) = SplitIdName(trimmed);
                if (id is ushort productId && name is not null)
                    currentVendor.Products[productId] = name;
            }
            else
            {
                var (id, name) = SplitIdName(line.AsSpan());
                if (id is ushort vid && name is not null)
                {
                    currentVendor = new VendorEntry(name);
                    result[vid] = currentVendor;
                }
            }
        }

        return result;
    }

    private static (ushort? Id, string? Name) SplitIdName(ReadOnlySpan<char> line)
    {
        // Format: "1234  Some name" (4 hex digits, two spaces, name)
        if (line.Length < 6) return (null, null);
        if (!ushort.TryParse(line[..4], System.Globalization.NumberStyles.HexNumber, null, out var id))
            return (null, null);
        var rest = line[4..].TrimStart();
        return (id, rest.IsEmpty ? null : rest.ToString());
    }

    private sealed class VendorEntry(string name)
    {
        public string Name { get; } = name;
        public Dictionary<ushort, string> Products { get; } = new();
    }
}
