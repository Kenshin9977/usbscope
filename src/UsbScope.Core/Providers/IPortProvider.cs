using UsbScope.Core.Models;

namespace UsbScope.Core.Providers;

/// Enumerates USB-C ports and the device sitting on each. Phase 1 reads
/// SetupAPI / WMI topology. Phase 2 will add a UcsiPortProvider that
/// fills cable info via the kernel filter driver. Both can run together;
/// the aggregator merges by port id.
public interface IPortProvider
{
    string Name { get; }

    /// Does this provider apply on the current host? Lets us skip
    /// vendor providers when their WMI namespace is missing.
    ValueTask<bool> IsAvailableAsync(CancellationToken ct = default);

    IAsyncEnumerable<UsbPort> EnumeratePortsAsync(CancellationToken ct = default);
}
