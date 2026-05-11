using WhatCable.Core.Models;

namespace WhatCable.Core.Providers;

/// Surfaces OEM-specific WMI/MOF data — Dell `DCIM_*`, Lenovo `Lenovo_*`,
/// etc. Implementations should fail closed (return null / empty) on
/// machines that aren't theirs, never throw.
public interface IVendorProvider
{
    string VendorName { get; }
    ValueTask<bool> IsAvailableAsync(CancellationToken ct = default);

    /// Returns vendor-specific properties to merge into a port's
    /// RawProperties map. Key format: "vendor.dell.AcAdapterWatts".
    ValueTask<IReadOnlyDictionary<string, string>> EnrichAsync(
        string portId,
        CancellationToken ct = default);
}
