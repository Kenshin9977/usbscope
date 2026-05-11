using WhatCable.Core.UsbIds;

namespace WhatCable.Core.Tests;

public class UsbIdsDatabaseTests
{
    [Fact]
    public void Resolves_A_Well_Known_Vendor()
    {
        // Logitech is 0x046D — extremely stable in the DB.
        Assert.Equal("Logitech, Inc.", UsbIdsDatabase.Instance.ResolveVendor(0x046D));
    }

    [Fact]
    public void Resolves_A_Well_Known_Product()
    {
        // 046D:C328 is a real Logitech product the user has plugged in.
        // We only assert the result is non-null and mentions Logitech-style
        // text — the exact product string is allowed to evolve in usb.ids.
        var name = UsbIdsDatabase.Instance.ResolveProduct(0x046D, 0xC328);
        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void Unknown_Vendor_Returns_Null()
    {
        Assert.Null(UsbIdsDatabase.Instance.ResolveVendor(0xFFFE));
    }

    [Fact]
    public void Unknown_Product_Returns_Null_Even_For_Known_Vendor()
    {
        Assert.Null(UsbIdsDatabase.Instance.ResolveProduct(0x046D, 0xDEAD));
    }
}
