using System.Buffers.Binary;
using UsbScope.Core.Models;

namespace UsbScope.Core.Billboard;

/// Parses a raw BOS (Binary Device Object Store) descriptor and
/// extracts the Billboard Capability descriptor when present. Pure
/// userspace logic — no Windows dependency — so it can be unit-tested
/// without any USB hardware.
///
/// BOS layout per USB 3.2 spec §9.6.2 (Binary Device Object Store):
///   bLength (1) | bDescriptorType=0x0F (1) | wTotalLength (2) | bNumDeviceCaps (1)
/// followed by bNumDeviceCaps capability descriptors, each starting with:
///   bLength (1) | bDescriptorType=0x10 (1) | bDevCapabilityType (1) | ...
///
/// Billboard cap (USB-IF Billboard spec 1.21): bDevCapabilityType=0x0D, 44 bytes
/// base + 4 bytes per Alternate Mode entry.
public static class BillboardParser
{
    public const byte BosDescriptorType         = 0x0F;
    public const byte DeviceCapabilityType      = 0x10;
    public const byte BillboardCapabilityType   = 0x0D;
    public const int  BillboardMinLength        = 44;
    public const int  AlternateModeEntrySize    = 4;

    /// Walk the BOS bytes, find the Billboard capability, return a
    /// decoded `BillboardInfo`. Returns null when the input isn't a
    /// well-formed BOS or no Billboard cap is present.
    public static BillboardInfo? TryParse(ReadOnlySpan<byte> bos)
    {
        if (bos.Length < 5) return null;
        if (bos[1] != BosDescriptorType) return null;

        var totalLength = BinaryPrimitives.ReadUInt16LittleEndian(bos.Slice(2, 2));
        if (totalLength < 5 || totalLength > bos.Length) return null;

        var numCaps = bos[4];
        var offset  = 5;

        for (var i = 0; i < numCaps && offset + 3 <= totalLength; i++)
        {
            var capLength = bos[offset];
            if (capLength < 3 || offset + capLength > totalLength) break;
            var capType = bos[offset + 2];

            if (bos[offset + 1] == DeviceCapabilityType && capType == BillboardCapabilityType)
                return ParseBillboard(bos.Slice(offset, capLength));

            offset += capLength;
        }

        return null;
    }

    private static BillboardInfo? ParseBillboard(ReadOnlySpan<byte> cap)
    {
        if (cap.Length < BillboardMinLength) return null;

        // Offset map for Billboard Capability Descriptor:
        //  0  bLength
        //  1  bDescriptorType (0x10)
        //  2  bDevCapabilityType (0x0D)
        //  3  iAdditionalInfoURL
        //  4  bNumberOfAlternateModes
        //  5  bPreferredAlternateMode
        //  6  VCONN_Power (USHORT)
        //  8  bmConfigured[32]
        // 40  bcdVersion (USHORT)
        // 42  bAdditionalFailureInfo
        // 43  bReserved
        // 44  AlternateMode[N], 4 bytes each
        var numAltModes        = cap[4];
        var preferredAltMode   = cap[5];
        var bcdVersion         = BinaryPrimitives.ReadUInt16LittleEndian(cap.Slice(40, 2));
        var additionalFailInfo = cap[42];

        var alts = new List<AlternateModeAdvert>(numAltModes);
        var altOffset = 44;
        for (var i = 0; i < numAltModes; i++)
        {
            if (altOffset + AlternateModeEntrySize > cap.Length) break;
            var svid = BinaryPrimitives.ReadUInt16LittleEndian(cap.Slice(altOffset, 2));
            var alt  = cap[altOffset + 2];
            // cap[altOffset + 3] is iAlternateModeString — string index,
            // not surfaced (we don't read string descriptors yet).
            alts.Add(new AlternateModeAdvert
            {
                Svid = svid,
                AlternateMode = alt,
                SvidName = AltModeSvidRegistry.Resolve(svid),
            });
            altOffset += AlternateModeEntrySize;
        }

        return new BillboardInfo
        {
            NumberOfAlternateModes  = numAltModes,
            PreferredAlternateMode  = preferredAltMode,
            BcdVersion              = bcdVersion,
            AdditionalFailureInfo   = additionalFailInfo,
            AlternateModes          = alts,
        };
    }
}
