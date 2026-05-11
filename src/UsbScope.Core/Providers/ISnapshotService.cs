using UsbScope.Core.Models;

namespace UsbScope.Core.Providers;

/// Orchestrates one read across all registered providers and returns
/// a single PortSnapshot. The UI and the CLI both go through this.
public interface ISnapshotService
{
    ValueTask<PortSnapshot> CaptureAsync(CancellationToken ct = default);
}
