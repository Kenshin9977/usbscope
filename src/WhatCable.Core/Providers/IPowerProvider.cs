using WhatCable.Core.Models;

namespace WhatCable.Core.Providers;

/// Returns power info for a given port. Phase 1 returns a host-wide
/// observed charge rate via battery WMI (we cannot attribute it to a
/// specific port without UCSI), and leaves negotiated PDO fields null.
public interface IPowerProvider
{
    string Name { get; }
    ValueTask<bool> IsAvailableAsync(CancellationToken ct = default);
    ValueTask<PowerInfo> ReadAsync(string portId, CancellationToken ct = default);
}
