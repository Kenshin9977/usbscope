using UsbScope.Core.Billboard;

namespace UsbScope.Core.Tests.Billboard;

public class BillboardParserTests
{
    [Fact]
    public void Returns_Null_For_Empty_Buffer()
    {
        Assert.Null(BillboardParser.TryParse([]));
    }

    [Fact]
    public void Returns_Null_When_Not_A_Bos_Descriptor()
    {
        var bytes = new byte[] { 9, 2, 0, 0, 0 };  // looks like a config desc, not BOS
        Assert.Null(BillboardParser.TryParse(bytes));
    }

    [Fact]
    public void Returns_Null_When_Bos_Has_No_Billboard_Cap()
    {
        // BOS with one non-Billboard capability (USB 2.0 extension, type 0x02).
        var bos = new byte[]
        {
            // BOS header
            5, 0x0F, 12, 0, 1,
            // single cap: USB 2.0 EXTENSION (7 bytes, cap type 0x02)
            7, 0x10, 0x02, 0x02, 0x00, 0x00, 0x00,
        };
        Assert.Null(BillboardParser.TryParse(bos));
    }

    [Fact]
    public void Parses_Single_DisplayPort_AltMode_Dock()
    {
        var bos = BuildBos([BuildBillboardCap(altModes:
        [
            (Svid: 0xFF01, AlternateMode: 1),  // DisplayPort pin assignment A
        ], bcdVersion: 0x0121)]);

        var info = BillboardParser.TryParse(bos);

        Assert.NotNull(info);
        Assert.Equal(1, info!.NumberOfAlternateModes);
        Assert.Equal((ushort)0x0121, info.BcdVersion);
        Assert.Equal("1.21", info.BcdVersionString);
        Assert.Single(info.AlternateModes);
        Assert.Equal((ushort)0xFF01, info.AlternateModes[0].Svid);
        Assert.Equal((byte)1, info.AlternateModes[0].AlternateMode);
        Assert.Equal("VESA DisplayPort", info.AlternateModes[0].SvidName);
    }

    [Fact]
    public void Parses_Tb4_Dock_With_DisplayPort_And_Thunderbolt()
    {
        var bos = BuildBos([BuildBillboardCap(altModes:
        [
            (Svid: 0xFF01, AlternateMode: 4),  // DisplayPort
            (Svid: 0x8087, AlternateMode: 1),  // Intel Thunderbolt
        ], bcdVersion: 0x0121, preferredAltMode: 1)]);

        var info = BillboardParser.TryParse(bos);

        Assert.NotNull(info);
        Assert.Equal(2, info!.NumberOfAlternateModes);
        Assert.Equal((byte)1, info.PreferredAlternateMode);
        Assert.Equal("VESA DisplayPort", info.AlternateModes[0].SvidName);
        Assert.Equal("Intel Thunderbolt", info.AlternateModes[1].SvidName);
    }

    [Fact]
    public void Unknown_Svid_Has_Null_Name_But_Is_Still_Surfaced()
    {
        var bos = BuildBos([BuildBillboardCap(altModes:
        [
            (Svid: 0xABCD, AlternateMode: 7),
        ])]);

        var info = BillboardParser.TryParse(bos);

        Assert.NotNull(info);
        Assert.Equal((ushort)0xABCD, info!.AlternateModes[0].Svid);
        Assert.Null(info.AlternateModes[0].SvidName);
    }

    [Fact]
    public void Skips_Non_Billboard_Caps_Before_Finding_Billboard()
    {
        // BOS with a USB 2.0 EXTENSION cap *then* a Billboard cap.
        var ext = new byte[] { 7, 0x10, 0x02, 0x02, 0x00, 0x00, 0x00 };
        var bb  = BuildBillboardCap(altModes: [(Svid: 0xFF01, AlternateMode: 1)]);
        var bos = BuildBos([ext, bb]);

        var info = BillboardParser.TryParse(bos);

        Assert.NotNull(info);
        Assert.Equal("VESA DisplayPort", info!.AlternateModes[0].SvidName);
    }

    // --- helpers ---

    private static byte[] BuildBos(byte[][] caps)
    {
        var capsTotal = caps.Sum(c => c.Length);
        var total = 5 + capsTotal;
        var bos = new byte[total];
        bos[0] = 5;
        bos[1] = 0x0F;
        bos[2] = (byte)(total & 0xFF);
        bos[3] = (byte)((total >> 8) & 0xFF);
        bos[4] = (byte)caps.Length;
        var offset = 5;
        foreach (var c in caps)
        {
            Array.Copy(c, 0, bos, offset, c.Length);
            offset += c.Length;
        }
        return bos;
    }

    private static byte[] BuildBillboardCap(
        (ushort Svid, byte AlternateMode)[] altModes,
        ushort bcdVersion = 0x0121,
        byte preferredAltMode = 0)
    {
        var length = 44 + 4 * altModes.Length;
        var cap = new byte[length];
        cap[0] = (byte)length;
        cap[1] = 0x10;                 // DEVICE CAPABILITY
        cap[2] = 0x0D;                 // BILLBOARD
        cap[3] = 0;                    // iAdditionalInfoURL
        cap[4] = (byte)altModes.Length;
        cap[5] = preferredAltMode;
        // 6-7: VCONN_Power (0)
        // 8-39: bmConfigured (0)
        cap[40] = (byte)(bcdVersion & 0xFF);
        cap[41] = (byte)((bcdVersion >> 8) & 0xFF);
        // 42: AdditionalFailureInfo (0)
        // 43: Reserved (0)
        var off = 44;
        foreach (var alt in altModes)
        {
            cap[off]     = (byte)(alt.Svid & 0xFF);
            cap[off + 1] = (byte)((alt.Svid >> 8) & 0xFF);
            cap[off + 2] = alt.AlternateMode;
            cap[off + 3] = 0;          // iAlternateModeString
            off += 4;
        }
        return cap;
    }
}

public class AltModeSvidRegistryTests
{
    [Theory]
    [InlineData((ushort)0xFF01, "VESA DisplayPort")]
    [InlineData((ushort)0x8087, "Intel Thunderbolt")]
    [InlineData((ushort)0x17EF, "Lenovo")]
    [InlineData((ushort)0x413C, "Dell")]
    public void Resolves_Well_Known_Svids(ushort svid, string expected)
    {
        Assert.Equal(expected, AltModeSvidRegistry.Resolve(svid));
    }

    [Fact]
    public void Unknown_Svid_Returns_Null()
    {
        Assert.Null(AltModeSvidRegistry.Resolve(0x1234));
    }

    [Fact]
    public void Describe_Includes_Hex_For_Unknown_Svids()
    {
        Assert.Equal("SVID 0x1234", AltModeSvidRegistry.Describe(0x1234));
        Assert.Equal("VESA DisplayPort (0xFF01)", AltModeSvidRegistry.Describe(0xFF01));
    }
}
