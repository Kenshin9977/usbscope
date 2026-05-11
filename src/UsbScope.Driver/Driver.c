//
// Driver.c — UsbScope kernel driver entry point.
//
// Phase 2 scaffold: software-only KMDF driver that creates a control
// device (\\.\UsbScope) and responds to a small IOCTL surface. The
// IOCTL responses are skeleton — IOCTL_USBSCOPE_GET_UCSI_STATE returns
// NumberOfConnectors=0 — until the UCM-UCSI filter attachment is
// wired in the next iteration.
//
// The driver installs as a kernel service (no PnP device), started on
// demand via `sc start UsbScope`. This keeps the install footprint
// minimal: no INF needed for dev/test signing.
//

#include <ntddk.h>
#include <wdf.h>
#include "Public.h"

// Forward declarations for our entry points.
DRIVER_INITIALIZE                DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD        UsbScopeEvtDriverDeviceAdd;
EVT_WDF_OBJECT_CONTEXT_CLEANUP   UsbScopeEvtDriverContextCleanup;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL UsbScopeEvtIoDeviceControl;

//
// DriverEntry — registered by the I/O manager when the driver loads.
// We hand control to WDF immediately; KMDF then calls our
// EvtDriverDeviceAdd to create the control device.
//
NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT  DriverObject,
    _In_ PUNICODE_STRING RegistryPath
)
{
    WDF_DRIVER_CONFIG     config;
    WDF_OBJECT_ATTRIBUTES attributes;

    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.EvtCleanupCallback = UsbScopeEvtDriverContextCleanup;

    WDF_DRIVER_CONFIG_INIT(&config, UsbScopeEvtDriverDeviceAdd);
    config.DriverPoolTag = 'csbU';  // 'Ubsc' tag in pool tracker

    NTSTATUS status = WdfDriverCreate(
        DriverObject,
        RegistryPath,
        &attributes,
        &config,
        WDF_NO_HANDLE);

    return status;
}

VOID
UsbScopeEvtDriverContextCleanup(
    _In_ WDFOBJECT Driver
)
{
    UNREFERENCED_PARAMETER(Driver);
    // Nothing to free yet — placeholder for when we add a UCSI cache.
}

//
// EvtDriverDeviceAdd — called once at driver load. Creates a single
// control device with a known name + symlink so userspace can open
// `\\.\UsbScope` and send IOCTLs.
//
NTSTATUS
UsbScopeEvtDriverDeviceAdd(
    _In_    WDFDRIVER       Driver,
    _Inout_ PWDFDEVICE_INIT DeviceInit
)
{
    UNREFERENCED_PARAMETER(Driver);

    DECLARE_CONST_UNICODE_STRING(deviceName, USBSCOPE_NT_DEVICE_NAME);
    DECLARE_CONST_UNICODE_STRING(symlinkName, USBSCOPE_SYMBOLIC_LINK_NAME);

    NTSTATUS status = WdfDeviceInitAssignName(DeviceInit, &deviceName);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    // Restrict access to administrators. Reading USB topology isn't
    // sensitive on its own, but we'll eventually expose PD/cable info
    // and want a clear authority boundary.
    WdfDeviceInitSetIoType(DeviceInit, WdfDeviceIoBuffered);

    WDFDEVICE device;
    status = WdfDeviceCreate(&DeviceInit, WDF_NO_OBJECT_ATTRIBUTES, &device);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    status = WdfDeviceCreateSymbolicLink(device, &symlinkName);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchSequential);
    queueConfig.EvtIoDeviceControl = UsbScopeEvtIoDeviceControl;

    WDFQUEUE queue;
    status = WdfIoQueueCreate(device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, &queue);
    return status;
}

//
// EvtIoDeviceControl — dispatch for our IOCTLs. Keep handlers small;
// real work (UCSI cache, VDO marshalling) lives in helper functions
// that get added as the filter behavior lands.
//
VOID
UsbScopeEvtIoDeviceControl(
    _In_ WDFQUEUE   Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t     OutputBufferLength,
    _In_ size_t     InputBufferLength,
    _In_ ULONG      IoControlCode
)
{
    UNREFERENCED_PARAMETER(Queue);
    UNREFERENCED_PARAMETER(InputBufferLength);

    NTSTATUS   status = STATUS_INVALID_DEVICE_REQUEST;
    ULONG_PTR  bytesReturned = 0;

    switch (IoControlCode) {

    case IOCTL_USBSCOPE_PING: {
        if (OutputBufferLength < sizeof(USBSCOPE_PING_RESPONSE)) {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }
        USBSCOPE_PING_RESPONSE* response;
        status = WdfRequestRetrieveOutputBuffer(
            Request, sizeof(USBSCOPE_PING_RESPONSE), (PVOID*)&response, NULL);
        if (!NT_SUCCESS(status)) break;

        response->Version = USBSCOPE_DRIVER_VERSION;
        // Capabilities advertise what we *can* deliver in this driver
        // build. UCSI_STATE bit is off until we ship the filter logic;
        // userspace will see the bit absent and surface a diagnostic.
        response->Capabilities = 0;
        bytesReturned = sizeof(USBSCOPE_PING_RESPONSE);
        status = STATUS_SUCCESS;
        break;
    }

    case IOCTL_USBSCOPE_GET_UCSI_STATE: {
        if (OutputBufferLength < sizeof(USBSCOPE_UCSI_STATE)) {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }
        USBSCOPE_UCSI_STATE* state;
        status = WdfRequestRetrieveOutputBuffer(
            Request, sizeof(USBSCOPE_UCSI_STATE), (PVOID*)&state, NULL);
        if (!NT_SUCCESS(status)) break;

        RtlZeroMemory(state, sizeof(USBSCOPE_UCSI_STATE));
        // Scaffold: zero connectors. The UCM-UCSI filter attachment
        // (next iteration) populates this from intercepted UCSI
        // GET_CONNECTOR_STATUS responses.
        state->NumberOfConnectors = 0;
        bytesReturned = sizeof(USBSCOPE_UCSI_STATE);
        status = STATUS_SUCCESS;
        break;
    }

    case IOCTL_USBSCOPE_GET_DISCOVER_IDENTITY: {
        if (OutputBufferLength < sizeof(USBSCOPE_DISCOVER_IDENTITY)) {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }
        USBSCOPE_DISCOVER_IDENTITY* idData;
        status = WdfRequestRetrieveOutputBuffer(
            Request, sizeof(USBSCOPE_DISCOVER_IDENTITY), (PVOID*)&idData, NULL);
        if (!NT_SUCCESS(status)) break;

        RtlZeroMemory(idData, sizeof(USBSCOPE_DISCOVER_IDENTITY));
        // Scaffold: no-response. Real Phase 3 implementation issues
        // a UCSI GET_PD_MESSAGE for the SOP' Discover Identity reply.
        idData->ResponseType = 3;  // no response
        bytesReturned = sizeof(USBSCOPE_DISCOVER_IDENTITY);
        status = STATUS_SUCCESS;
        break;
    }

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
        break;
    }

    WdfRequestCompleteWithInformation(Request, status, bytesReturned);
}
