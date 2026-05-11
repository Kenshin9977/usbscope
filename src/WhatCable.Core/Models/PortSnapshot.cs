namespace WhatCable.Core.Models;

/// One frozen read of all ports, suitable for JSON serialization
/// (CLI output) or binding to the UI.
public sealed record PortSnapshot
{
    public required DateTimeOffset CapturedAt { get; init; }
    public required string HostMachine { get; init; }
    public required IReadOnlyList<UsbPort> Ports { get; init; }
    public IReadOnlyList<string> ProviderNames { get; init; } = [];

    /// Soft warnings: a provider was queried, threw, and we degraded.
    /// Useful in --json and in engineer mode.
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
