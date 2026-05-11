namespace UsbScope.Core.Pd;

/// Decodes a raw USB-PD Discover Identity ACK message into a
/// `DiscoverIdentity` record. Input is the sequence of 32-bit VDO
/// words after the Message Header — as captured by UCSI
/// GET_PD_MESSAGE in Phase 2.
///
/// Per USB-PD 3.1 §6.4.4.3.1 the sequence is:
///   [0] ID Header VDO
///   [1] Cert Stat VDO
///   [2] Product VDO
///   [3..5] Product-Type-specific VDOs (0..3 of them depending on type)
///
/// A SOP' response from a cable e-marker carries the Passive Cable
/// VDO (PD 3.1) or Active Cable VDO (PD 3.0) at index 3.
public static class DiscoverIdentityDecoder
{
    /// Decode a Discover Identity response. Returns null when the
    /// payload is too short to be valid (less than 3 VDOs — header,
    /// cert, product are always required).
    ///
    /// `cameFromCableSop` should be true when this response was
    /// obtained via a SOP' message (the cable plug answering). In
    /// that case ProductTypeVdo[0] is the Passive/Active Cable VDO
    /// and we surface it on the result.
    public static DiscoverIdentity? Decode(ReadOnlySpan<uint> vdos, bool cameFromCableSop = false)
    {
        if (vdos.Length < 3)
            return null;

        var header  = new IdHeaderVdo(vdos[0]);
        var cert    = new CertStatVdo(vdos[1]);
        var product = new ProductVdo(vdos[2]);

        var typeVdos = vdos.Length > 3 ? vdos[3..].ToArray() : [];

        PassiveCableVdo? passiveCable = null;
        if (cameFromCableSop && typeVdos.Length >= 1)
        {
            // For PD 3.1, SOP' from a passive cable plug puts the
            // Passive Cable VDO at PT-VDO[0]. Active cables put an
            // Active Cable VDO there instead — same first 32 bits
            // worth of layout for the fields we care about.
            passiveCable = new PassiveCableVdo(typeVdos[0]);
        }

        return new DiscoverIdentity
        {
            IdHeader = header,
            CertStat = cert,
            Product  = product,
            ProductTypeVdos = typeVdos,
            PassiveCable = passiveCable,
        };
    }
}
