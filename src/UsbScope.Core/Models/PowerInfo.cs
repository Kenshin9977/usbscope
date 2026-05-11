namespace UsbScope.Core.Models;

/// What we know about power flow on a port right now. Most fields are
/// nullable because Phase 1 will surface partial info: charge rate is
/// reachable through WMI battery classes, but the negotiated PDO and
/// PD contract require UCSI (Phase 2).
public sealed record PowerInfo
{
    public PowerRole Role { get; init; } = PowerRole.Unknown;

    /// Negotiated voltage in millivolts. Null until UCSI lands.
    public int? NegotiatedVoltageMv { get; init; }

    /// Negotiated current in milliamps. Null until UCSI lands.
    public int? NegotiatedCurrentMa { get; init; }

    /// Convenience: negotiated power in watts when both V and I are known.
    public double? NegotiatedPowerW =>
        NegotiatedVoltageMv is int v && NegotiatedCurrentMa is int i
            ? v * i / 1_000_000.0
            : null;

    /// Observed instantaneous charge rate from the battery driver, in watts.
    /// Userspace can read this; it is what we use in Phase 1 to detect
    /// "your charger is fine but the cable is throttling you".
    public double? ObservedChargeRateW { get; init; }

    /// Maximum power the partner is advertising. Phase 2.
    public double? PartnerAdvertisedMaxW { get; init; }
}
