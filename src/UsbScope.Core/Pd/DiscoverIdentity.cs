namespace UsbScope.Core.Pd;

/// Decoded USB-PD Discover Identity response. Built by
/// `DiscoverIdentityDecoder.Decode` from raw 32-bit VDO words returned
/// by the UsbScope kernel driver (Phase 2) or by Linux's
/// `/sys/class/typec/.../identity` on systems we test against.
///
/// `ProductTypeVdo0` is interpreted based on the ID Header's
/// ProductType{Ufp,Dfp}. For passive cables that's PassiveCableVdo;
/// for hubs / peripherals / VPDs, different decoders apply.
public sealed record DiscoverIdentity
{
    public required IdHeaderVdo IdHeader { get; init; }
    public required CertStatVdo CertStat { get; init; }
    public required ProductVdo Product { get; init; }

    /// 0–3 product-type-specific VDOs (per PD 3.1 only PT-VDO[0] is
    /// universally present; some types add up to 2 more).
    public IReadOnlyList<uint> ProductTypeVdos { get; init; } = [];

    /// Cable e-marker information, decoded from ProductTypeVdo[0] when
    /// ProductTypeUfp == Vpd or when this response came from a SOP'
    /// query (cable plug answering on the wire). Null otherwise.
    public PassiveCableVdo? PassiveCable { get; init; }

    /// True when this Discover Identity describes a USB-C cable rather
    /// than the device on the other end (i.e., the response came from
    /// SOP' / SOP'' rather than from the partner device).
    public bool IsCable => PassiveCable is not null;
}
