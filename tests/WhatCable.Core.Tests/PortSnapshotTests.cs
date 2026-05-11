using WhatCable.Core.Models;

namespace WhatCable.Core.Tests;

public class PortSnapshotTests
{
    [Fact]
    public void NegotiatedPowerW_Computes_From_Voltage_And_Current()
    {
        var info = new PowerInfo { NegotiatedVoltageMv = 20_000, NegotiatedCurrentMa = 5_000 };
        Assert.Equal(100.0, info.NegotiatedPowerW);
    }

    [Fact]
    public void NegotiatedPowerW_Is_Null_When_Either_Side_Is_Missing()
    {
        Assert.Null(new PowerInfo { NegotiatedVoltageMv = 20_000 }.NegotiatedPowerW);
        Assert.Null(new PowerInfo { NegotiatedCurrentMa = 5_000 }.NegotiatedPowerW);
        Assert.Null(new PowerInfo().NegotiatedPowerW);
    }

    [Fact]
    public void PortSnapshot_Requires_Captured_Host_And_Ports()
    {
        var s = new PortSnapshot
        {
            CapturedAt = DateTimeOffset.UnixEpoch,
            HostMachine = "host",
            Ports = new List<UsbPort>(),
        };
        Assert.Equal("host", s.HostMachine);
        Assert.Empty(s.Ports);
    }
}
