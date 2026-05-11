using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace UsbScope.Providers.Ucsi;

/// Low-level IPC with the UsbScope kernel driver. Opens
/// `\\.\UsbScope` once, sends IOCTLs, marshals the structs.
///
/// Lifetime: one per snapshot. Holds an open handle to the driver's
/// control device for the duration. Safe to dispose multiple times.
[SupportedOSPlatform("windows10.0.22621.0")]
internal sealed partial class UcsiDriverClient : IDisposable
{
    private const uint GENERIC_READ    = 0x80000000;
    private const uint GENERIC_WRITE   = 0x40000000;
    private const uint FILE_SHARE_READ  = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING   = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const int  INVALID_HANDLE_VALUE  = -1;

    private const int ERROR_FILE_NOT_FOUND = 2;
    private const int ERROR_ACCESS_DENIED  = 5;

    private readonly SafeFileHandle? _handle;

    /// True when the driver is installed and we have a usable handle.
    public bool IsConnected => _handle is { IsInvalid: false, IsClosed: false };

    /// The Win32 error from CreateFile when the open failed. 0 if no
    /// open was attempted or the open succeeded. We expose this so
    /// callers can distinguish "driver not installed" (ERROR_FILE_NOT_FOUND)
    /// from "access denied" (need elevation) etc.
    public int OpenError { get; }

    public UcsiDriverClient()
    {
        var h = NativeMethods.CreateFile(
            UcsiIoctl.UserDevicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (h.IsInvalid)
        {
            OpenError = Marshal.GetLastWin32Error();
            h.Dispose();
            _handle = null;
        }
        else
        {
            _handle = h;
        }
    }

    /// Human-readable explanation of OpenError, used in diagnostics.
    public string OpenErrorMessage => OpenError switch
    {
        0 => "ok",
        ERROR_FILE_NOT_FOUND =>
            "UsbScope driver not installed. See src/UsbScope.Driver/README.md to build and load it.",
        ERROR_ACCESS_DENIED =>
            "Access denied opening UsbScope driver — run elevated or check the driver's security descriptor.",
        _ => $"CreateFile(\\\\.\\UsbScope) failed with Win32 error {OpenError}.",
    };

    public bool TryPing(out USBSCOPE_PING_RESPONSE response)
    {
        response = default;
        return _handle is { } h && SendIoctl(h, UcsiIoctl.IOCTL_USBSCOPE_PING, ref response);
    }

    public bool TryGetUcsiState(out USBSCOPE_UCSI_STATE state)
    {
        state = default;
        return _handle is { } h && SendIoctl(h, UcsiIoctl.IOCTL_USBSCOPE_GET_UCSI_STATE, ref state);
    }

    public bool TryGetDiscoverIdentity(byte connectorIndex, out USBSCOPE_DISCOVER_IDENTITY data)
    {
        data = default;
        if (_handle is not { } h) return false;
        // Input buffer carries the connector index; output buffer carries the response.
        return SendIoctlInOut(h, UcsiIoctl.IOCTL_USBSCOPE_GET_DISCOVER_IDENTITY, connectorIndex, ref data);
    }

    private static unsafe bool SendIoctl<T>(SafeFileHandle h, uint code, ref T output) where T : unmanaged
    {
        fixed (T* p = &output)
        {
            uint bytesReturned = 0;
            return NativeMethods.DeviceIoControl(
                h, code,
                IntPtr.Zero, 0,
                (IntPtr)p, (uint)sizeof(T),
                out bytesReturned,
                IntPtr.Zero);
        }
    }

    private static unsafe bool SendIoctlInOut<T>(
        SafeFileHandle h, uint code, byte input, ref T output) where T : unmanaged
    {
        var inputBuffer = stackalloc byte[1];
        inputBuffer[0] = input;
        fixed (T* p = &output)
        {
            uint bytesReturned = 0;
            return NativeMethods.DeviceIoControl(
                h, code,
                (IntPtr)inputBuffer, 1,
                (IntPtr)p, (uint)sizeof(T),
                out bytesReturned,
                IntPtr.Zero);
        }
    }

    public void Dispose() => _handle?.Dispose();

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        public static partial SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);
    }
}
