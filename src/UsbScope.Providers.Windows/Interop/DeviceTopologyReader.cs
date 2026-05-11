using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;

namespace UsbScope.Providers.Windows.Interop;

/// Reads parent/port topology data from the Configuration Manager.
/// We use it to map a USB device to (its parent hub, its port number
/// on that hub) so UsbHubReader can IOCTL the right hub and read the
/// authoritative negotiated speed.
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class DeviceTopologyReader
{
    private static readonly DEVPROPKEY DevPkeyDeviceParent = new()
    {
        fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7),
        pid = 8,
    };

    private static readonly DEVPROPKEY DevPkeyDeviceAddress = new()
    {
        fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
        pid = 30,
    };

    public readonly record struct Topology(string? ParentInstanceId, uint? PortAddress);

    public static Topology Read(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return default;

        uint devInst;
        unsafe
        {
            fixed (char* p = instanceId)
            {
                var rc = PInvoke.CM_Locate_DevNode(out devInst, new PWSTR(p), 0);
                if (rc != CONFIGRET.CR_SUCCESS)
                    return default;
            }
        }

        var parent = ReadString(devInst, DevPkeyDeviceParent);
        var address = ReadUInt32(devInst, DevPkeyDeviceAddress);
        return new Topology(parent, address);
    }

    private static unsafe string? ReadString(uint devInst, DEVPROPKEY key)
    {
        DEVPROPTYPE type = 0;
        uint size = 0;
        PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, null, ref size, 0);
        if (size == 0 || size > 4096)
            return null;

        Span<byte> buffer = stackalloc byte[(int)size];
        fixed (byte* p = buffer)
        {
            var rc = PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, p, ref size, 0);
            if (rc != CONFIGRET.CR_SUCCESS)
                return null;
        }
        var chars = MemoryMarshal.Cast<byte, char>(buffer);
        var len = chars.IndexOf('\0');
        return len < 0 ? new string(chars) : new string(chars[..len]);
    }

    private static unsafe uint? ReadUInt32(uint devInst, DEVPROPKEY key)
    {
        DEVPROPTYPE type = 0;
        uint size = 0;
        PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, null, ref size, 0);
        if (size != 4)
            return null;

        Span<byte> buffer = stackalloc byte[4];
        fixed (byte* p = buffer)
        {
            var rc = PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, p, ref size, 0);
            if (rc != CONFIGRET.CR_SUCCESS)
                return null;
        }
        return (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
    }
}
