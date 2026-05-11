# UsbScope kernel driver (Phase 2 scaffold)

A KMDF software-only driver that exposes a control device (`\\.\UsbScope`) and a small IOCTL surface to its userspace client. Currently a skeleton: the IOCTLs reply but the data they return is stubbed. The next iteration attaches it as an upper filter on the UCM-UCSI ACPI client device to populate `IOCTL_USBSCOPE_GET_UCSI_STATE` from real intercepted UCSI traffic.

## What's here

| File                 | Role                                                                 |
|----------------------|----------------------------------------------------------------------|
| `Public.h`           | IOCTL contract shared with the userspace client                       |
| `Driver.c`           | DriverEntry, EvtDriverDeviceAdd, EvtIoDeviceControl                  |
| `UsbScope.Driver.vcxproj` | KMDF project file, targets x64 + ARM64, Spectre-mitigated        |

## Prerequisites

1. **Windows Driver Kit (WDK)** matching the Windows SDK on the box. Install via winget:
   ```powershell
   winget install Microsoft.WindowsWDK.10.0.26100
   ```
2. **Visual Studio 2022** with the C++ Native Desktop workload.
3. **MSVC Spectre-mitigated libraries** (Visual Studio Installer → Individual components → `MSVC v143 - VS 2022 C++ x64/x86 Spectre-mitigated libs`). Without these, the build fails with `LNK1104: cannot open file 'libcmt.lib'` (Spectre variant).
4. **Test signing enabled** on the target machine for dev installs:
   ```powershell
   bcdedit /set testsigning on
   ```
   Reboot. A "Test Mode" watermark appears on the desktop until you turn it off.

## Build (developer flow)

```powershell
cd src\UsbScope.Driver
msbuild UsbScope.Driver.vcxproj /p:Configuration=Debug /p:Platform=x64
```

Output goes to `x64\Debug\UsbScope.Driver\` and contains `UsbScope.sys` plus a generated test certificate. The certificate is regenerated each build — fine for dev, **don't ship it**.

## Install (dev / test signing)

The driver installs as a kernel service, no INF needed (it has no PnP device to bind to):

```powershell
sc.exe create UsbScope binPath= "C:\path\to\UsbScope.sys" type= kernel start= demand
sc.exe start UsbScope
```

Verify it loaded:

```powershell
sc.exe query UsbScope
Get-CimInstance Win32_SystemDriver -Filter "Name='UsbScope'"
```

Userspace then opens `\\.\UsbScope` via `CreateFile`.

## Uninstall

```powershell
sc.exe stop UsbScope
sc.exe delete UsbScope
```

## Production signing (for distribution outside dev machines)

1. Acquire an **EV code signing certificate** (Certum SimplySign EV ≈ €140/yr, DigiCert ≈ €400/yr — both work). Standard non-EV certs **do not** work for kernel-mode on Windows 10 1607+.
2. Sign `UsbScope.sys` with the EV cert.
3. Submit to the **Microsoft Hardware Dev Center / Partner Center** for attestation signing. Microsoft re-signs with its own cert. Free, automated, takes hours not days.
4. Distribute the re-signed `.sys`. Users no longer need test signing mode.

WHQL certification (HLK testing, Windows Update distribution) is a separate, heavier process — only needed for very wide rollout.

## Architecture roadmap

This driver is intentionally minimal in its first cut. The expansion path:

1. **Current (scaffold)**: control device + stub IOCTL handlers.
2. **Filter attach**: register as an upper filter on UCM-UCSI ACPI client devices via INF (`AddReg ... UpperFilters`). Intercept I/O, snoop UCSI command responses (`GET_CONNECTOR_STATUS`, `GET_PDOS`, `GET_CABLE_PROPERTY`), cache in a per-connector struct.
3. **State exposure**: `IOCTL_USBSCOPE_GET_UCSI_STATE` returns the live cache.
4. **Discover Identity** (Phase 3): driver issues its own `GET_PD_MESSAGE` commands for the SOP' Discover Identity response and returns the raw VDOs to userspace, which decodes them via the `VdoDecoder` in `UsbScope.Core`.

The control-device-only design means the driver is useful even on machines whose UCM-UCSI stack we haven't filtered (the GET_UCSI_STATE returns zero connectors, GET_DISCOVER_IDENTITY returns "no response"). Userspace handles those cases gracefully.
