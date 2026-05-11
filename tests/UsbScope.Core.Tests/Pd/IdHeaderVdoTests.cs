using UsbScope.Core.Pd;

namespace UsbScope.Core.Tests.Pd;

/// Verifies bit extraction in the ID Header VDO. Inputs are constructed
/// from our decoder's field positions (round-trip test). Re-validating
/// against real captured VDOs is a follow-up once we have Phase 2 driver
/// captures from real UCSI hardware.
public class IdHeaderVdoTests
{
    [Fact]
    public void Decodes_Apple_USB4_Cable_Pattern()
    {
        // Synthetic Apple (VID 0x05AC) USB-C cable plug, captive,
        // VCONN-powered (VPD), no USB comms, no modal operation.
        // Bits: 31:30=00 host/dev comms off, 29:27=101 VPD,
        //       26=0, 25:23=000 DFP undefined,
        //       22:21=11 captive plug, 15:0=0x05AC.
        uint raw =
            (5u << 27) |       // ProductTypeUfp = 5 (VPD)
            ((uint)ConnectorType.UsbTypeCPlug << 21) |
            0x05ACu;

        var h = new IdHeaderVdo(raw);

        Assert.Equal(0x05AC, h.VendorId);
        Assert.Equal(ProductTypeUfp.Vpd, h.ProductTypeUfp);
        Assert.Equal(ProductTypeDfp.Undefined, h.ProductTypeDfp);
        Assert.Equal(ConnectorType.UsbTypeCPlug, h.ConnectorType);
        Assert.False(h.UsbCommunicationsCapableAsHost);
        Assert.False(h.UsbCommunicationsCapableAsDevice);
        Assert.False(h.ModalOperationSupported);
    }

    [Fact]
    public void Decodes_Anker_Charger_Pattern()
    {
        // Power Brick (DFP type 3), Anker-ish vendor 0x291A.
        // Bits: 31:30=00, 25:23=011 PowerBrick, 22:21=10 receptacle.
        uint raw =
            ((uint)ProductTypeDfp.PowerBrick << 23) |
            ((uint)ConnectorType.UsbTypeCReceptacle << 21) |
            0x291Au;

        var h = new IdHeaderVdo(raw);

        Assert.Equal(0x291A, h.VendorId);
        Assert.Equal(ProductTypeDfp.PowerBrick, h.ProductTypeDfp);
        Assert.Equal(ConnectorType.UsbTypeCReceptacle, h.ConnectorType);
    }

    [Fact]
    public void Detects_Modal_Operation_Bit()
    {
        var with    = new IdHeaderVdo(1u << 26);
        var without = new IdHeaderVdo(0);
        Assert.True(with.ModalOperationSupported);
        Assert.False(without.ModalOperationSupported);
    }
}
