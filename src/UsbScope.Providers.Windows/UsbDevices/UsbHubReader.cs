using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using UsbScope.Core.Models;
using UsbScope.Providers.Windows.Interop;

namespace UsbScope.Providers.Windows.UsbDevices;

/// Opens each USB hub through its kernel device interface and talks
/// IOCTL to read authoritative per-port connection info. This is the
/// only reliable way to get the negotiated speed of a USB device on
/// Windows from userspace — DEVPROPKEY readers can't reach it.
///
/// Lifetime: a single instance per snapshot. Hub handles are opened
/// lazily on first use and disposed by the caller via IDisposable.
[SupportedOSPlatform("windows10.0.22621.0")]
internal sealed class UsbHubReader : IDisposable
{
    // GENERIC_WRITE — CsWin32 doesn't expose this as a const in every
    // metadata version, and we only need the bit.
    private const uint GENERIC_WRITE = 0x40000000;

    // Map hub *device instance id* → hub *interface path*. The instance
    // id is what DEVPKEY_Device_Parent reports on child devices; the
    // interface path is what CreateFile needs.
    private readonly Dictionary<string, string> _hubInterfaceByInstance;

    // Lazy-opened handles to each hub. Concurrent because the enumerator
    // is async.
    private readonly ConcurrentDictionary<string, SafeFileHandle?> _hubHandles = new();

    public UsbHubReader()
    {
        _hubInterfaceByInstance = EnumerateHubs();
    }

    /// Returns the negotiated speed of the device sitting on
    /// `(hubInstanceId, portNumber)`, or `Unknown` if we can't reach
    /// the hub, the port is empty, or the IOCTL fails.
    public UsbDataRate GetNegotiatedSpeed(string hubInstanceId, uint portNumber)
    {
        if (string.IsNullOrEmpty(hubInstanceId) || portNumber == 0)
            return UsbDataRate.Unknown;

        var handle = GetHandle(hubInstanceId);
        if (handle is null || handle.IsInvalid)
            return UsbDataRate.Unknown;

        var info = ReadConnectionInfo(handle, portNumber);
        if (info is null) return UsbDataRate.Unknown;

        return MapSpeed(info.Value.Speed, info.Value.DeviceDescriptor);
    }

    /// Reads the USB Billboard descriptor of the device on
    /// `(hubInstanceId, portNumber)`. Returns null when the device
    /// doesn't advertise Billboard (the common case for keyboards,
    /// mice, storage, etc.) or when the BOS GET_DESCRIPTOR control
    /// transfer doesn't complete.
    public Core.Models.BillboardInfo? TryReadBillboard(string hubInstanceId, uint portNumber)
    {
        if (string.IsNullOrEmpty(hubInstanceId) || portNumber == 0)
            return null;

        var handle = GetHandle(hubInstanceId);
        if (handle is null || handle.IsInvalid)
            return null;

        return BillboardReader.TryRead(handle, portNumber);
    }

    private SafeFileHandle? GetHandle(string hubInstanceId)
    {
        return _hubHandles.GetOrAdd(hubInstanceId, key =>
        {
            if (!_hubInterfaceByInstance.TryGetValue(key, out var interfacePath))
                return null;
            return OpenHub(interfacePath);
        });
    }

    private static SafeFileHandle? OpenHub(string interfacePath)
    {
        try
        {
            var handle = PInvoke.CreateFile(
                interfacePath,
                GENERIC_WRITE,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                lpSecurityAttributes: null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                hTemplateFile: null);

            return handle.IsInvalid ? null : handle;
        }
        catch
        {
            return null;
        }
    }

    private static unsafe USB_NODE_CONNECTION_INFORMATION_EX? ReadConnectionInfo(
        SafeFileHandle hub,
        uint portNumber)
    {
        var info = new USB_NODE_CONNECTION_INFORMATION_EX { ConnectionIndex = portNumber };
        var size = (uint)Marshal.SizeOf<USB_NODE_CONNECTION_INFORMATION_EX>();
        uint returned = 0;

        var ok = PInvoke.DeviceIoControl(
            hub,
            UsbIoctl.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX,
            &info, size,
            &info, size,
            &returned,
            null);
        if (!ok || returned == 0)
            return null;

        // ConnectionStatus 1 = DeviceConnected; anything else means no
        // device on this port and the descriptor bytes are stale.
        return info.ConnectionStatus == 1 ? info : null;
    }

    /// Translate the USB_DEVICE_SPEED byte + bcdUSB into our enum.
    /// Speed=3 means SuperSpeed at the link layer; the device may
    /// still be USB 3.2 Gen 2 (10 Gb/s) or Gen 2x2 (20 Gb/s) — the
    /// IOCTL_..._EX_V2 variant disambiguates, but is not universally
    /// available. We use bcdUSB as a tiebreaker.
    private static UsbDataRate MapSpeed(byte speed, USB_DEVICE_DESCRIPTOR desc) => speed switch
    {
        UsbIoctl.UsbLowSpeed   => UsbDataRate.LowSpeed1_5Mbps,
        UsbIoctl.UsbFullSpeed  => UsbDataRate.FullSpeed12Mbps,
        UsbIoctl.UsbHighSpeed  => UsbDataRate.HighSpeed480Mbps,
        UsbIoctl.UsbSuperSpeed => desc.bcdUSB >= 0x0320
            ? UsbDataRate.SuperSpeedPlus10Gbps
            : UsbDataRate.SuperSpeed5Gbps,
        _ => UsbDataRate.Unknown,
    };

    private static Dictionary<string, string> EnumerateHubs()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var hubGuid = PInvoke.GUID_DEVINTERFACE_USB_HUB;
        uint length = 0;
        var rcSize = PInvoke.CM_Get_Device_Interface_List_Size(
            out length, hubGuid, default(PWSTR),
            CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
        if (rcSize != CONFIGRET.CR_SUCCESS || length < 2)
            return result;

        // The list is a double-null-terminated array of wide strings.
        // CsWin32 generates PZZWSTR as a separate type; we keep it as
        // raw chars in a Span and let the implicit operator wire it up.
        var buffer = new char[length];
        unsafe
        {
            fixed (char* p = buffer)
            {
                var rc = PInvoke.CM_Get_Device_Interface_List(
                    hubGuid,
                    default(PWSTR),
                    new PZZWSTR(p),
                    length,
                    CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
                if (rc != CONFIGRET.CR_SUCCESS)
                    return result;
            }
        }

        int start = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != '\0') continue;
            if (i == start) break; // double-null = end of list
            var interfacePath = new string(buffer, start, i - start);
            var instanceId = QueryInterfaceInstanceId(interfacePath);
            if (!string.IsNullOrEmpty(instanceId))
                result[instanceId] = interfacePath;
            start = i + 1;
        }

        return result;
    }

    private static unsafe string? QueryInterfaceInstanceId(string interfacePath)
    {
        // DEVPKEY_Device_InstanceId on a *device interface* — the GUID
        // here is the interface-property family ({78c34fc8-...}), with
        // pid 256 for the instance id. This is different from the
        // device-property family.
        var key = new DEVPROPKEY
        {
            fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57),
            pid = 256,
        };
        DEVPROPTYPE type = 0;
        uint size = 0;

        PInvoke.CM_Get_Device_Interface_Property(
            interfacePath, in key, out type, null, ref size, 0);
        if (size == 0 || size > 2048)
            return null;

        Span<byte> buf = stackalloc byte[(int)size];
        fixed (byte* pBuf = buf)
        {
            var rc = PInvoke.CM_Get_Device_Interface_Property(
                interfacePath, in key, out type, pBuf, ref size, 0);
            if (rc != CONFIGRET.CR_SUCCESS)
                return null;
        }
        var chars = MemoryMarshal.Cast<byte, char>(buf);
        var len = chars.IndexOf('\0');
        return len < 0 ? new string(chars) : new string(chars[..len]);
    }

    public void Dispose()
    {
        foreach (var handle in _hubHandles.Values)
            handle?.Dispose();
        _hubHandles.Clear();
    }
}
