using UsbScope.Core.Pd;

namespace UsbScope.Core.Tests.Pd;

public class PassiveCableVdoTests
{
    [Fact]
    public void Decodes_Usb4_Gen3_5A_Cable_Pattern()
    {
        // Build a passive cable VDO for a typical Thunderbolt 4 / USB4
        // Gen 3 cable: 20 Gbps per pair, 5 A, 20 V max, ≤10 ns latency.
        uint raw =
            ((uint)CableUsbSpeed.Usb4Gen3 << 11)        |
            ((uint)CableVbusCurrent.FiveAmps << 9)      |
            ((uint)CableVbusMaxVoltage.TwentyVolts << 5)|
            ((uint)CableTerminationType.Passive << 3)   |
            1u;  // latency code 1 = ≤10 ns

        var c = new PassiveCableVdo(raw);

        Assert.Equal(CableUsbSpeed.Usb4Gen3, c.UsbHighestSpeed);
        Assert.Equal(CableVbusCurrent.FiveAmps, c.VbusCurrent);
        Assert.Equal(CableVbusMaxVoltage.TwentyVolts, c.VbusMaxVoltage);
        Assert.Equal(CableTerminationType.Passive, c.TerminationType);
        Assert.Equal(1, c.LatencyCode);
        Assert.Equal(10, c.LatencyNanosecondsApprox);
    }

    [Fact]
    public void Decodes_Usb2_Only_Cheap_Cable_Pattern()
    {
        // 0x01 = latency 1 only, everything else zero → USB 2.0 only,
        // 3A capable (Reserved value 0 → "reserved" — but real cables
        // would report 1 or 2). We verify zero values decode predictably.
        var c = new PassiveCableVdo(1u);

        Assert.Equal(CableUsbSpeed.Usb2Only, c.UsbHighestSpeed);
        Assert.Equal(CableVbusCurrent.Reserved, c.VbusCurrent);
        Assert.Equal(CableTerminationType.Passive, c.TerminationType);
    }
}

public class CertStatVdoTests
{
    [Fact]
    public void Zero_Xid_Means_Uncertified()
    {
        var c = new CertStatVdo(0);
        Assert.False(c.IsUsbIfCertified);
        Assert.Equal(0u, c.Xid);
    }

    [Fact]
    public void Nonzero_Xid_Decoded_As_Is()
    {
        var c = new CertStatVdo(0xDEADBEEF);
        Assert.True(c.IsUsbIfCertified);
        Assert.Equal(0xDEADBEEFu, c.Xid);
    }
}

public class ProductVdoTests
{
    [Fact]
    public void Splits_Pid_And_BcdDevice()
    {
        // USB PID 0x0123, bcdDevice 0x0205 ("2.05")
        var p = new ProductVdo(0x01230205u);

        Assert.Equal((ushort)0x0123, p.ProductId);
        Assert.Equal((ushort)0x0205, p.BcdDevice);
        Assert.Equal("2.05", p.BcdDeviceString);
    }
}

public class DiscoverIdentityDecoderTests
{
    [Fact]
    public void Returns_Null_For_Too_Short_Payload()
    {
        Assert.Null(DiscoverIdentityDecoder.Decode(new uint[] { 1, 2 }));
    }

    [Fact]
    public void Decodes_Sop_Prime_Response_With_Cable_Vdo()
    {
        // Synthetic SOP' response: header + cert + product + passive
        // cable VDO. The decoder is told this came from SOP' so it
        // surfaces PassiveCable.
        uint header  = (5u << 27) | ((uint)ConnectorType.UsbTypeCPlug << 21) | 0x05ACu;
        uint cert    = 0x12345678;
        uint product = 0x01230100;
        uint cable   = ((uint)CableUsbSpeed.Usb4Gen3 << 11)
                     | ((uint)CableVbusCurrent.FiveAmps << 9);

        var id = DiscoverIdentityDecoder.Decode(new[] { header, cert, product, cable }, cameFromCableSop: true);

        Assert.NotNull(id);
        Assert.True(id!.IsCable);
        Assert.Equal(0x05AC, id.IdHeader.VendorId);
        Assert.Equal(CableUsbSpeed.Usb4Gen3, id.PassiveCable!.Value.UsbHighestSpeed);
        Assert.Equal(CableVbusCurrent.FiveAmps, id.PassiveCable.Value.VbusCurrent);
    }

    [Fact]
    public void Sop_Response_From_Partner_Does_Not_Treat_PtVdo_As_Cable()
    {
        // Same payload but cameFromCableSop=false (this came from the
        // partner device, not its cable). PT-VDO is product-type-specific
        // (could be PD Hub VDO, etc.) — not a cable VDO.
        uint header  = (1u << 27) | 0x05ACu;  // PdUsbHub
        uint cert    = 0;
        uint product = 0;
        uint ptVdo   = 0xDEADBEEF;

        var id = DiscoverIdentityDecoder.Decode(new[] { header, cert, product, ptVdo }, cameFromCableSop: false);

        Assert.NotNull(id);
        Assert.False(id!.IsCable);
        Assert.Null(id.PassiveCable);
        Assert.Single(id.ProductTypeVdos);
        Assert.Equal(0xDEADBEEFu, id.ProductTypeVdos[0]);
    }
}
