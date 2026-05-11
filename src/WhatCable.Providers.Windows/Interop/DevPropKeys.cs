using System.Runtime.Versioning;
using Windows.Win32.Devices.Properties;

namespace WhatCable.Providers.Windows.Interop;

/// DEVPROPKEYs we need that aren't in the Win32 metadata. Defined from
/// usbioctl.h / devpkey.h shipped with the Windows SDK.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class DevPropKeys
{
    /// usbioctl.h — DEVPKEY_Device_UsbSpeed. Values: 0=LowSpeed, 1=FullSpeed,
    /// 2=HighSpeed, 3=SuperSpeed, 4=SuperSpeedPlus.
    public static readonly DEVPROPKEY UsbSpeed = new()
    {
        fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29),
        pid = 4,
    };

    /// usbioctl.h — DEVPKEY_Device_BusReportedDeviceDesc duplicate (already in metadata, kept for clarity)
    public static readonly DEVPROPKEY BusReportedDeviceDesc = new()
    {
        fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2),
        pid = 4,
    };
}
