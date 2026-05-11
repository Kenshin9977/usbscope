using System.Runtime.Versioning;
using WhatCable.Core.Models;
using WhatCable.Core.Providers;

namespace WhatCable.Providers.Windows;

/// Phase 1 snapshot orchestrator. Today this returns an empty snapshot;
/// the SetupAPI/WMI providers land in subsequent commits and plug in
/// here via the IPortProvider/IPowerProvider/IVendorProvider interfaces.
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class SnapshotService : ISnapshotService
{
    public ValueTask<PortSnapshot> CaptureAsync(CancellationToken ct = default)
    {
        var snapshot = new PortSnapshot
        {
            CapturedAt = DateTimeOffset.Now,
            HostMachine = Environment.MachineName,
            Ports = [],
            ProviderNames = [],
            Diagnostics = ["Phase 1 providers not yet implemented — scaffold only."],
        };
        return ValueTask.FromResult(snapshot);
    }
}
