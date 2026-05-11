using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;
using WhatCable.Core.Models;

namespace WhatCable.Providers.Windows.Interop;

/// Reads DEVPKEY_Device_UsbSpeed for a given device instance ID via
/// the Configuration Manager. Returns Unknown for anything we can't read,
/// never throws — this is best-effort enrichment.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class UsbSpeedReader
{
    public static UsbDataRate Read(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return UsbDataRate.Unknown;

        uint devInst;
        unsafe
        {
            fixed (char* p = instanceId)
            {
                var rc = PInvoke.CM_Locate_DevNode(out devInst, new PWSTR(p), 0);
                if (rc != CONFIGRET.CR_SUCCESS)
                    return UsbDataRate.Unknown;
            }
        }

        var key = DevPropKeys.UsbSpeed;
        uint size = 0;
        DEVPROPTYPE type = 0;

        unsafe
        {
            // First call sizes the buffer.
            PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, null, ref size, 0);
            if (size == 0)
                return UsbDataRate.Unknown;

            Span<byte> buffer = stackalloc byte[(int)size];
            fixed (byte* pBuf = buffer)
            {
                var rc = PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, pBuf, ref size, 0);
                if (rc != CONFIGRET.CR_SUCCESS || size < 4)
                    return UsbDataRate.Unknown;
            }

            // DEVPROP_TYPE_UINT32 — little-endian uint.
            uint speedCode = (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
            return MapUsbSpeed(speedCode);
        }
    }

    /// USB_DEVICE_SPEED from usbspec.h. The driver doesn't distinguish 10 vs 20 Gbps —
    /// both report as SuperSpeedPlus, so we conservatively map to 10 Gb/s here.
    private static UsbDataRate MapUsbSpeed(uint code) => code switch
    {
        0 => UsbDataRate.LowSpeed1_5Mbps,
        1 => UsbDataRate.FullSpeed12Mbps,
        2 => UsbDataRate.HighSpeed480Mbps,
        3 => UsbDataRate.SuperSpeed5Gbps,
        4 => UsbDataRate.SuperSpeedPlus10Gbps,
        _ => UsbDataRate.Unknown,
    };
}
