//
// Public.h — IOCTL contract shared between the UsbScope kernel driver
// and userspace callers. The C# client (UsbScope.Providers.Ucsi)
// keeps the same constants and struct layouts in sync by hand.
//

#pragma once

//
// Symbolic name used by userspace to open the driver:
//     CreateFile(L"\\\\.\\UsbScope", ...);
// The driver creates this symbolic link in EvtDriverDeviceAdd.
//
#define USBSCOPE_NT_DEVICE_NAME      L"\\Device\\UsbScope"
#define USBSCOPE_SYMBOLIC_LINK_NAME  L"\\DosDevices\\UsbScope"
#define USBSCOPE_USER_DEVICE_PATH    L"\\\\.\\UsbScope"

//
// Driver version. Format: (major << 16) | minor. Bump on any IOCTL
// contract change so the userspace client can refuse to talk to an
// incompatible driver build.
//
#define USBSCOPE_DRIVER_VERSION      0x00010000  // 1.0

//
// Capability bits returned by IOCTL_USBSCOPE_PING. The driver advertises
// what it can do; userspace gates feature use on these bits so we can
// ship a driver where a feature isn't yet wired without breaking the
// client.
//
#define USBSCOPE_CAP_UCSI_STATE      (1u << 0)   // GET_UCSI_STATE returns real data
#define USBSCOPE_CAP_DISCOVER_ID     (1u << 1)   // GET_DISCOVER_IDENTITY supported

//
// IOCTL codes. FILE_DEVICE_UNKNOWN + custom function range 0x800+ is
// the conventional layout for in-house drivers. METHOD_BUFFERED means
// the I/O manager copies buffers in/out for us — safer than direct
// at the cost of one extra copy, fine for low-rate control IO.
//
#ifndef CTL_CODE
#  include <winioctl.h>
#endif

#define USBSCOPE_IOCTL(fn, access) \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800 + (fn), METHOD_BUFFERED, (access))

#define IOCTL_USBSCOPE_PING                 USBSCOPE_IOCTL(0x01, FILE_READ_ACCESS)
#define IOCTL_USBSCOPE_GET_UCSI_STATE       USBSCOPE_IOCTL(0x02, FILE_READ_ACCESS)
#define IOCTL_USBSCOPE_GET_DISCOVER_IDENTITY USBSCOPE_IOCTL(0x03, FILE_READ_ACCESS)

//
// IOCTL_USBSCOPE_PING — fixed-size response.
//
#pragma pack(push, 1)

typedef struct _USBSCOPE_PING_RESPONSE {
    unsigned int Version;        // USBSCOPE_DRIVER_VERSION
    unsigned int Capabilities;   // bitmask of USBSCOPE_CAP_*
} USBSCOPE_PING_RESPONSE;

//
// IOCTL_USBSCOPE_GET_UCSI_STATE — one row per UCSI connector. The
// driver caches the most recent ConnectorStatus the UCM stack saw
// (Phase 2 filter behavior; in the current scaffold it returns
// NumberOfConnectors=0 until the filter logic lands).
//
typedef struct _USBSCOPE_CONNECTOR_STATUS {
    unsigned char  ConnectorIndex;          // 1-based, matches UCSI
    unsigned char  PowerOperationMode;      // UCSI PowerOperationMode: 0=Unknown, 1=USB Default, 2=BC, 3=PD, 4=Type-C 1.5A, 5=Type-C 3.0A
    unsigned char  ConnectStatus;           // 0=disconnected, 1=connected
    unsigned char  PowerDirection;          // 0=sink, 1=source
    unsigned int   NegotiatedVoltageMv;
    unsigned int   NegotiatedCurrentMa;
    unsigned char  PartnerType;             // UCSI PartnerType: 1=DFP, 2=UFP, 3=Cable+UFP, 4=Cable+nothing, 5=Debug, 6=Audio
    unsigned char  Reserved[3];
} USBSCOPE_CONNECTOR_STATUS;

typedef struct _USBSCOPE_UCSI_STATE {
    unsigned char            NumberOfConnectors;
    unsigned char            UcsiVersionMajor;     // from UCSI version register
    unsigned char            UcsiVersionMinor;
    unsigned char            Reserved;
    USBSCOPE_CONNECTOR_STATUS Connectors[8];       // hardware-bound max
} USBSCOPE_UCSI_STATE;

//
// IOCTL_USBSCOPE_GET_DISCOVER_IDENTITY — raw VDO words from the
// UCSI GET_PD_MESSAGE response. The userspace VDO decoder
// (UsbScope.Core / VdoDecoder) parses these per USB-PD 3.1.
//
typedef struct _USBSCOPE_DISCOVER_IDENTITY {
    unsigned char  ConnectorIndex;
    unsigned char  ResponseType;     // 0=ACK, 1=NAK, 2=BUSY, 3=no response
    unsigned char  Reserved[2];
    unsigned int   IdHeaderVdo;
    unsigned int   CertStatVdo;
    unsigned int   ProductVdo;
    unsigned int   ProductTypeVdos[3]; // up to 3 PT-VDOs depending on product type
} USBSCOPE_DISCOVER_IDENTITY;

#pragma pack(pop)
